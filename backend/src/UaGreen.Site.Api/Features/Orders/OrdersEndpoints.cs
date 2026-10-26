using System.Text;
using Microsoft.EntityFrameworkCore;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Domain;
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
    List<OrderLineRequest> Items, string? Lang, string? Website);

public sealed record LiqPayCheckoutDto(string CheckoutUrl, string Data, string Signature);
public sealed record OrderItemDto(string ProductName, string? OptionsText, int Qty, decimal UnitPrice, decimal LineTotal);
public sealed record OrderDto(Guid Id, int Number, string Status, string PaymentMethod, decimal Subtotal, decimal DeliveryCost,
    decimal Total, DateTime CreatedAt, IReadOnlyList<OrderItemDto> Items, LiqPayCheckoutDto? LiqPay);

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
            return ToDto(o, null);
        }).AllowAnonymous();

        app.MapPost("/api/payments/liqpay/callback", LiqPayCallbackAsync).AllowAnonymous().DisableAntiforgery();
    }

    private static async Task<IResult> CreateAsync(
        CreateOrderRequest req, AppDbContext db, LiqPayService liqpay, Notifier notifier, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(req.Website)) // bot: pretend it worked, store nothing
            return Results.Ok(new { id = Guid.NewGuid(), number = 0, status = "New" });

        var name = (req.Customer?.Name ?? "").Trim();
        var phone = (req.Customer?.Phone ?? "").Trim();
        if (name.Length is < 2 or > 120) throw ProblemException.BadRequest("Name is required");
        if (phone.Length is < 7 or > 30) throw ProblemException.BadRequest("Phone is required");
        if (!Enum.TryParse<PaymentMethod>(req.PaymentMethod, true, out var payment))
            throw ProblemException.BadRequest("Unknown payment method");
        if (!Enum.TryParse<DeliveryMethod>(req.Delivery?.Method, true, out var delivery))
            throw ProblemException.BadRequest("Unknown delivery method");
        if (req.Items is null) throw ProblemException.BadRequest("Order has no items");
        var lang = Lang.Norm(req.Lang);

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
                PaymentMethod.LiqPay => OrderStatus.AwaitingPayment,
                PaymentMethod.Credit => OrderStatus.Credit,
                _ => OrderStatus.New,
            },
            CustomerName = name,
            Phone = phone,
            Email = string.IsNullOrWhiteSpace(req.Customer!.Email) ? null : req.Customer.Email.Trim(),
            DeliveryMethod = delivery,
            DeliveryCity = req.Delivery!.City?.Trim(),
            DeliveryWarehouse = req.Delivery.Warehouse?.Trim(),
            Comment = req.Comment?.Trim(),
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

        LiqPayCheckoutDto? checkout = null;
        if (payment == PaymentMethod.LiqPay)
        {
            var (data, signature) = liqpay.BuildCheckout(order, lang);
            checkout = new LiqPayCheckoutDto(LiqPayService.CheckoutUrl, data, signature);
        }

        await notifier.NotifyManagersAsync($"Нове замовлення №{order.Number} — {order.Total:N0} грн", Describe(order), ct);
        return Results.Ok(ToDto(order, checkout));
    }

    private static async Task<IResult> LiqPayCallbackAsync(
        HttpRequest request, AppDbContext db, LiqPayService liqpay, Notifier notifier, ILoggerFactory lf, CancellationToken ct)
    {
        var log = lf.CreateLogger("LiqPay");
        var form = await request.ReadFormAsync(ct);
        string data = form["data"]!, signature = form["signature"]!;
        if (string.IsNullOrEmpty(data) || !liqpay.VerifySignature(data, signature))
            throw ProblemException.BadRequest("Invalid signature", ErrorCodes.InvalidSignature);

        var cb = LiqPayService.Decode(data) ?? throw ProblemException.BadRequest("Malformed data");
        if (!Guid.TryParse(cb.OrderId, out var orderId)) throw ProblemException.BadRequest("Unknown order");
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct) ?? throw ProblemException.NotFound("Order not found");

        order.LiqPayStatus = cb.Status;
        order.UpdatedAt = DateTime.UtcNow;
        if (LiqPayService.IsPaid(cb.Status) && order.Status != OrderStatus.Paid)
        {
            if (cb.Amount != order.Total || !string.Equals(cb.Currency, "UAH", StringComparison.OrdinalIgnoreCase))
            {
                // Signed, but not for what we charged — never auto-mark paid, let a human look.
                log.LogWarning("LiqPay amount mismatch for order {Number}: got {Amount} {Currency}, expected {Total}",
                    order.Number, cb.Amount, cb.Currency, order.Total);
            }
            else
            {
                order.Status = OrderStatus.Paid;
                order.PaidAt = DateTime.UtcNow;
                await notifier.NotifyManagersAsync($"Оплачено замовлення №{order.Number} — {order.Total:N0} грн",
                    $"LiqPay payment_id {cb.PaymentId}, status {cb.Status}.", ct);
            }
        }
        await db.SaveChangesAsync(ct);
        return Results.Ok();
    }

    public static OrderDto ToDto(Order o, LiqPayCheckoutDto? liqpay) => new(
        o.Id, o.Number, o.Status.ToString(), o.PaymentMethod.ToString(), o.Subtotal, o.DeliveryCost, o.Total, o.CreatedAt,
        o.Items.Select(i => new OrderItemDto(i.ProductName, i.OptionsText, i.Qty, i.UnitPrice, i.LineTotal)).ToList(), liqpay);

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
