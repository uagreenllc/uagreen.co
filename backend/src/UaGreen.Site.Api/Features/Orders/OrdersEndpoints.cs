using System.Text;
using Microsoft.EntityFrameworkCore;
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
    List<OrderLineRequest> Items, string? Lang, string? Website, string? Captcha);

public sealed record OrderItemDto(string ProductName, string? OptionsText, int Qty, decimal UnitPrice, decimal LineTotal);
public sealed record OrderDto(Guid Id, int Number, string Status, string PaymentMethod, decimal Subtotal, decimal DeliveryCost,
    decimal Total, DateTime CreatedAt, IReadOnlyList<OrderItemDto> Items, string? PayUrl, string? PaymentStatus,
    DateOnly? AuditDate, string? AuditAddress, bool? AuditConfirmed);

public static class OrdersEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/orders", CreateAsync).AllowAnonymous().RequireRateLimiting(RateLimitingExtensions.Forms);

        // Public status page (result_url). The Guid is the only secret; no customer data is returned.
        app.MapGet("/api/orders/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var o = await db.Orders.AsNoTracking().Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw ProblemException.NotFound("Order not found");
            return ToDto(o);
        }).AllowAnonymous();
    }

    private static async Task<IResult> CreateAsync(
        CreateOrderRequest req, AppDbContext db, Notifier notifier, Turnstile captcha, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(req.Website)) // bot: pretend it worked, store nothing
            return Results.Ok(new { id = Guid.NewGuid(), number = 0, status = "New" });

        var name = (req.Customer?.Name ?? "").Trim();
        var phone = (req.Customer?.Phone ?? "").Trim();
        if (name.Length is < 2 or > 120) throw ProblemException.BadRequest("Name is required");
        if (phone.Length is < 7 or > 30) throw ProblemException.BadRequest("Phone is required");
        if (!Enum.TryParse<PaymentMethod>(req.PaymentMethod, true, out var payment))
            throw ProblemException.BadRequest("Unknown payment method");
        var payOffKey = PaymentSettings.OffKey(payment); // set in admin → Платіжні системи
        if (await db.Settings.AnyAsync(s => s.Key == payOffKey && s.Value == PaymentSettings.Off, ct))
            throw ProblemException.BadRequest("Payment method is turned off");
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

        await notifier.NotifyManagersAsync($"Нове замовлення №{order.Number} — {order.Total:N0} грн", Describe(order), ct);
        return Results.Ok(ToDto(order));
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
