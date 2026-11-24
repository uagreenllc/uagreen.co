using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Features.Audit;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Features.Forms;
using UaGreen.Site.Api.Infrastructure.Captcha;
using UaGreen.Site.Api.Infrastructure.Email;
using UaGreen.Site.Api.Infrastructure.Payments;
using UaGreen.Site.Api.Infrastructure.Persistence;
using UaGreen.Site.Api.Infrastructure.RateLimiting;

namespace UaGreen.Site.Api.Features.Orders;

public sealed record CustomerDto(string Name, string Phone, string? Email);
public sealed record DeliveryDto(string Method, string? City, string? Warehouse);
/// `Website` is a honeypot: humans never see the field, bots fill it.
public sealed record CreateOrderRequest(
    CustomerDto Customer, DeliveryDto Delivery, string PaymentMethod, string? Comment,
    List<OrderLineRequest> Items, string? Lang, string? Website, string? Captcha, int? Parts = null);

public sealed record OrderItemDto(string ProductName, string? OptionsText, int Qty, decimal UnitPrice, decimal LineTotal);
public sealed record OrderDto(Guid Id, int Number, string Status, string PaymentMethod, decimal Subtotal, decimal DeliveryCost,
    decimal Total, DateTime CreatedAt, IReadOnlyList<OrderItemDto> Items, string? PayUrl, string? PaymentStatus,
    DateOnly? AuditDate, string? AuditAddress, bool? AuditConfirmed);

public static class OrdersEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/orders", CreateAsync).AllowAnonymous().RequireRateLimiting(RateLimitingExtensions.Forms);

        // Public status page (redirectUrl). The Guid is the only secret; no customer data is returned.
        // Re-reads the bank state while the order waits for it (the bank cannot call back to every environment).
        app.MapGet("/api/orders/{id:guid}", async (Guid id, AppDbContext db, PaymentSync sync, CancellationToken ct) =>
        {
            var o = await db.Orders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw ProblemException.NotFound("Order not found");
            await sync.RefreshIfStaleAsync(o, ct);
            return ToDto(o);
        }).AllowAnonymous();
    }

    private static async Task<IResult> CreateAsync(
        CreateOrderRequest req, AppDbContext db, Notifier notifier, Turnstile captcha, MonobankAcquiring card, MonoPartsClient parts,
        IOptions<MonobankOptions> cardOpts, IOptions<MonoPartsOptions> partsOpts, ILoggerFactory lf, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(req.Website)) // bot: pretend it worked, store nothing
            return Results.Ok(new { id = Guid.NewGuid(), number = 0, status = "New" });

        var name = (req.Customer?.Name ?? "").Trim();
        var phone = (req.Customer?.Phone ?? "").Trim();
        if (name.Length is < 2 or > 120) throw ProblemException.BadRequest("Name is required");
        if (phone.Length is < 7 or > 30) throw ProblemException.BadRequest("Phone is required");
        if (!Enum.TryParse<PaymentMethod>(req.PaymentMethod, true, out var payment))
            throw ProblemException.BadRequest("Unknown payment method");
        if (await MethodOffAsync(db, payment, cardOpts.Value, partsOpts.Value, ct))
            throw ProblemException.BadRequest("Payment method is turned off");
        string? partsPhone = null;
        int[]? partsCounts = null;
        if (payment == PaymentMethod.MonoParts)
        {
            partsPhone = MonoPartsClient.NormalizePhone(phone)
                ?? throw ProblemException.BadRequest("Для покупки частинами потрібен український номер +380…");
            var max = MonoPartsClient.MaxParts(
                await db.Settings.Where(s => s.Key == PaymentSettings.PartsMax).Select(s => s.Value).FirstOrDefaultAsync(ct));
            partsCounts = MonoPartsClient.PartsFor(req.Parts, max)
                ?? throw ProblemException.BadRequest($"Кількість платежів — від 3 до {max}");
        }
        if (!Enum.TryParse<DeliveryMethod>(req.Delivery?.Method, true, out var delivery))
            throw ProblemException.BadRequest("Unknown delivery method");
        var offKey = $"delivery.{delivery}.off"; // set in admin → Доставка
        if (await db.Settings.AnyAsync(s => s.Key == offKey, ct))
            throw ProblemException.BadRequest("Delivery method is turned off");
        if (req.Items is null) throw ProblemException.BadRequest("Order has no items");
        var lang = Lang.Norm(req.Lang);
        await captcha.EnsureHumanAsync(req.Captcha, ct);

        var ids = req.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking()
            .Include(p => p.Options).ThenInclude(o => o.Values)
            .Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var (subtotal, items) = OrderPricing.Compute(req.Items, products, lang);

        var now = DateTime.UtcNow;
        var order = new Order
        {
            Id = Guid.NewGuid(),
            Status = payment switch
            {
                PaymentMethod.Card or PaymentMethod.MonoParts => OrderStatus.AwaitingPayment,
                PaymentMethod.Credit => OrderStatus.Credit,
                _ => OrderStatus.New,
            },
            CustomerName = name,
            Phone = phone,
            Email = FormsEndpoints.Trimmed(req.Customer!.Email),
            DeliveryMethod = delivery,
            DeliveryCity = FormsEndpoints.Trimmed(req.Delivery!.City),
            DeliveryWarehouse = FormsEndpoints.Trimmed(req.Delivery.Warehouse),
            Comment = FormsEndpoints.Trimmed(req.Comment, 2000),
            PaymentMethod = payment,
            Subtotal = subtotal,
            DeliveryCost = 0,
            Total = subtotal,
            CreatedAt = now,
            UpdatedAt = now,
            Items = items,
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        // The order is saved before the bank is asked: a bank failure never loses it.
        string? bankNote = null;
        if (payment == PaymentMethod.Card)
            bankNote = await TryNewInvoiceAsync(order, card, lf, ct);
        else if (payment == PaymentMethod.MonoParts)
        {
            bankNote = req.Parts is { } n ? $"Покупка частинами: {n} платежів" : null;
            try
            {
                order.PaymentRef = await parts.CreateAsync(order, partsPhone!, partsCounts!, ct);
                order.PaymentStatus = "IN_PROCESS/WAITING_FOR_CLIENT";
            }
            catch (MonoPartsException ex)
            {
                order.Status = OrderStatus.New; // the manual path: a manager calls the customer
                order.PaymentStatus = "FAIL/CREATE_ERROR";
                bankNote = $"{bankNote}\nmonobank не прийняв заявку на покупку частинами: {ex.Message}".Trim();
            }
        }
        if (payment is PaymentMethod.Card or PaymentMethod.MonoParts) await db.SaveChangesAsync(ct);

        await notifier.NotifyManagersAsync($"Нове замовлення №{order.Number} — {order.Total:N0} грн",
            Describe(order) + (bankNote is null ? "" : $"\n{bankNote}\n"), ct);
        return Results.Ok(ToDto(order));
    }

    /// Off in the admin, or its bank keys are missing on the server.
    public static async Task<bool> MethodOffAsync(
        AppDbContext db, PaymentMethod m, MonobankOptions card, MonoPartsOptions parts, CancellationToken ct)
    {
        if (PaymentSettings.Unconfigured(card, parts).Contains(m)) return true;
        var key = PaymentSettings.OffKey(m);
        return await db.Settings.AnyAsync(s => s.Key == key && s.Value == PaymentSettings.Off, ct);
    }

    /// Points the order at a fresh hosted payment page. Throws MonobankException.
    public static async Task NewInvoiceAsync(Order o, MonobankAcquiring card, CancellationToken ct)
    {
        var inv = await card.CreateInvoiceAsync(o, ct);
        o.PaymentRef = inv.InvoiceId;
        o.PayUrl = inv.PageUrl;
        o.PaymentStatus = null;
    }

    /// At checkout: a bank failure leaves the order without a page; "Оплатити" on the order page retries.
    public static async Task<string?> TryNewInvoiceAsync(Order o, MonobankAcquiring card, ILoggerFactory lf, CancellationToken ct)
    {
        try
        {
            await NewInvoiceAsync(o, card, ct);
            return null;
        }
        catch (MonobankException ex)
        {
            lf.CreateLogger("monobank").LogWarning("invoice/create for order {Number} failed: {Error}", o.Number, ex.Message);
            return $"monobank не створив рахунок: {ex.Message} (клієнт може натиснути «Оплатити» на сторінці замовлення)";
        }
    }

    public static OrderDto ToDto(Order o) => new(
        o.Id, o.Number, o.Status.ToString(), o.PaymentMethod.ToString(), o.Subtotal, o.DeliveryCost, o.Total, o.CreatedAt,
        o.Items.Select(i => new OrderItemDto(i.ProductName, i.OptionsText, i.Qty, i.UnitPrice, i.LineTotal)).ToList(), o.PayUrl, o.PaymentStatus,
        o.AuditDate, o.AuditAddress, o.AuditDate is null ? null : o.AuditDateWasFree == true);

    private static string Describe(Order o)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Замовлення №{o.Number} ({o.Status}, оплата: {o.PaymentMethod})");
        sb.AppendLine($"Клієнт: {o.CustomerName}, {o.Phone}{(o.Email is null ? "" : ", " + o.Email)}");
        sb.AppendLine($"Доставка: {o.DeliveryMethod} {o.DeliveryCity} {o.DeliveryWarehouse}".Trim());
        if (!string.IsNullOrEmpty(o.Comment)) sb.AppendLine($"Коментар: {o.Comment}");
        sb.AppendLine();
        foreach (var i in o.Items)
            sb.AppendLine($"- {i.ProductName}{(i.OptionsText is null ? "" : $" ({i.OptionsText})")} × {i.Qty} = {i.LineTotal:N0} грн");
        sb.AppendLine($"Разом: {o.Total:N0} грн");
        return sb.ToString();
    }
}
