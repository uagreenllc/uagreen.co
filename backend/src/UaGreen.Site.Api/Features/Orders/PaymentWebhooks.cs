using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Payments;
using UaGreen.Site.Api.Infrastructure.Persistence;
using UaGreen.Site.Api.Infrastructure.RateLimiting;

namespace UaGreen.Site.Api.Features.Orders;

/// monobank calls back here; the body only tells us which order to re-read (see PaymentSync).
public static class PaymentWebhooks
{
    public static void Map(IEndpointRouteBuilder app)
    {
        // Rate-limited with the chat policy (60/min per IP): plenty for the bank, cheap for anyone else.
        app.MapPost("/api/payments/monobank/webhook", (HttpRequest r, AppDbContext db, PaymentSync sync, ILoggerFactory lf, CancellationToken ct)
            => WakeAsync(r, "invoiceId", db, sync, lf, ct)).AllowAnonymous().DisableAntiforgery().RequireRateLimiting(RateLimitingExtensions.Chat);
        app.MapPost("/api/payments/monoparts/callback", (HttpRequest r, AppDbContext db, PaymentSync sync, ILoggerFactory lf, CancellationToken ct)
            => WakeAsync(r, "order_id", db, sync, lf, ct)).AllowAnonymous().DisableAntiforgery().RequireRateLimiting(RateLimitingExtensions.Chat);

        // "Оплатити" on the order page: the current invoice while it can still take the payment, else a new one.
        app.MapPost("/api/orders/{id:guid}/pay", async (Guid id, AppDbContext db, PaymentSync sync, MonobankAcquiring card, CancellationToken ct) =>
        {
            var o = await db.Orders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw ProblemException.NotFound("Order not found");
            await sync.RefreshIfStaleAsync(o, ct);
            if (o.PaymentMethod != PaymentMethod.Card || o.Status != OrderStatus.AwaitingPayment)
                throw ProblemException.BadRequest("Замовлення вже не очікує оплати карткою");
            if (o.PayUrl is null || !MonobankAcquiring.Reusable(o.PaymentStatus))
            {
                try
                {
                    await OrdersEndpoints.NewInvoiceAsync(o, card, ct);
                }
                catch (MonobankException)
                {
                    throw new ProblemException(502, "BANK_UNAVAILABLE", "Не вдалося зв'язатися з monobank, спробуйте пізніше");
                }
                o.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            return Results.Ok(new { payUrl = o.PayUrl });
        }).AllowAnonymous().RequireRateLimiting(RateLimitingExtensions.Chat);
    }

    /// Always 200: the bank must not retry a call we chose to ignore.
    private static async Task<IResult> WakeAsync(
        HttpRequest request, string field, AppDbContext db, PaymentSync sync, ILoggerFactory lf, CancellationToken ct)
    {
        if (request.ContentLength > 65536) return Results.Ok();
        using var reader = new StreamReader(request.Body);
        var id = ExtractId(await reader.ReadToEndAsync(ct), field);
        var o = id is null ? null : await db.Orders.FirstOrDefaultAsync(x => x.PaymentRef == id, ct);
        if (o is null) return Results.Ok();
        try
        {
            await sync.RefreshAsync(o, ct);
        }
        catch (Exception ex) when (ex is MonobankException or MonoPartsException)
        {
            lf.CreateLogger("monobank").LogWarning("Webhook for order {Number}: status read failed: {Error}", o.Number, ex.Message);
        }
        return Results.Ok();
    }

    /// The string value of a top-level field, or null for anything else (not JSON, missing, not a string, empty).
    public static string? ExtractId(string body, string field)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty(field, out var v) && v.ValueKind == JsonValueKind.String
                && v.GetString() is { Length: > 0 and <= 64 } s ? s : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
