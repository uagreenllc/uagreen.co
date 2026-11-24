using Microsoft.EntityFrameworkCore;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Features.Orders;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Captcha;
using UaGreen.Site.Api.Infrastructure.Email;
using UaGreen.Site.Api.Infrastructure.Payments;
using UaGreen.Site.Api.Infrastructure.Persistence;
using UaGreen.Site.Api.Infrastructure.RateLimiting;

namespace UaGreen.Site.Api.Features.Audit;

/// `Website` is a honeypot: humans never see the field, bots fill it.
public sealed record AuditConsultationDto(string Phone, string? Website, string? Captcha);

public sealed record AuditSlotsDto(DateOnly From, DateOnly To, IReadOnlyList<DateOnly> Dates, decimal Price, decimal? OldPrice, bool Unavailable);

public sealed record AuditBookingRequest(string Name, string Phone, string? Email, string Address, string? Comment,
    DateOnly Date, bool OfferAccepted, string? Lang, string? Website, string? Captcha);

/// The site-audit flow at the end of the K5 story: a phone-only consultation request,
/// and (see AuditBooking) a paid audit booked into Gospodar.
public static class AuditEndpoints
{
    public const string ConsultationText = "Консультація щодо аудиту ділянки";

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/audit/consultation", async (
            AuditConsultationDto req, AppDbContext db, Notifier notifier, Turnstile captcha, CancellationToken ct) =>
        {
            if (!string.IsNullOrEmpty(req.Website)) return Results.Ok(new { ok = true });
            var phone = (req.Phone ?? "").Trim();
            if (phone.Length is < 7 or > 30) throw ProblemException.BadRequest("Phone is required");
            await captcha.EnsureHumanAsync(req.Captcha, ct);

            var c = new ContactRequest
            {
                Id = Guid.NewGuid(),
                Phone = phone,
                Message = ConsultationText,
                CreatedAt = DateTime.UtcNow,
            };
            // CrmSyncWorker pushes every ContactRequest to Gospodar's CRM (with retries); nothing to send here.
            db.ContactRequests.Add(c);
            await db.SaveChangesAsync(ct);
            await notifier.NotifyManagersAsync($"Консультація щодо аудиту: {phone}", $"{phone}\n\n{ConsultationText}", ct);
            return Results.Ok(new { ok = true });
        }).AllowAnonymous().RequireRateLimiting(RateLimitingExtensions.Forms);

        // ponytail: no cache — Gospodar is asked on every page view; add output cache if it shows up in load.
        app.MapGet("/api/audit/slots", async (AppDbContext db, GospodarClient gospodar, CancellationToken ct) =>
        {
            var product = await AuditProductAsync(db, ct);
            var (from, to) = AuditWindow.For(DateTime.UtcNow);
            var dates = await gospodar.GetAuditSlotsAsync(from, to, ct);
            return new AuditSlotsDto(from, to, dates?.Where(d => d >= from && d <= to).ToList() ?? [],
                product.Price, product.OldPrice, dates is null);
        }).AllowAnonymous();

        app.MapPost("/api/audit/bookings", CreateBookingAsync).AllowAnonymous().RequireRateLimiting(RateLimitingExtensions.Forms);
    }

    private static async Task<IResult> CreateBookingAsync(
        AuditBookingRequest req, AppDbContext db, GospodarClient gospodar, LiqPayService liqpay, Notifier notifier, Turnstile captcha,
        CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(req.Website)) // bot: pretend it worked, store nothing
            return Results.Ok(new { id = Guid.NewGuid(), number = 0, status = "New" });

        var name = (req.Name ?? "").Trim();
        var phone = (req.Phone ?? "").Trim();
        var address = (req.Address ?? "").Trim();
        var comment = string.IsNullOrWhiteSpace(req.Comment) ? null : req.Comment.Trim();
        if (name.Length is < 2 or > 120) throw ProblemException.BadRequest("Name is required");
        if (phone.Length is < 7 or > 30) throw ProblemException.BadRequest("Phone is required");
        if (address.Length is < 5 or > 300) throw ProblemException.BadRequest("Address is required");
        if (comment?.Length > 1000) throw ProblemException.BadRequest("Comment is too long");
        if (!req.OfferAccepted) throw ProblemException.BadRequest("Offer must be accepted");
        await captcha.EnsureHumanAsync(req.Captcha, ct);

        var now = DateTime.UtcNow;
        var (from, to) = AuditWindow.For(now);
        var free = await gospodar.GetAuditSlotsAsync(from, to, ct);
        if (!AuditWindow.IsBookable(req.Date, now, free ?? []))
            throw ProblemException.BadRequest("Date is not available", ErrorCodes.DateUnavailable);

        var product = await AuditProductAsync(db, ct);
        var lang = Lang.Norm(req.Lang);
        var order = new Order
        {
            Id = Guid.NewGuid(),
            Status = OrderStatus.AwaitingPayment,
            CustomerName = name,
            Phone = phone,
            Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim(),
            DeliveryMethod = DeliveryMethod.Pickup, // nothing ships; never shown for an audit
            Comment = comment,
            PaymentMethod = PaymentMethod.LiqPay,
            Subtotal = product.Price,
            Total = product.Price,
            AuditDate = req.Date,
            AuditAddress = address,
            OfferAcceptedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
            Items =
            [
                new OrderItem
                {
                    Id = Guid.NewGuid(), ProductId = product.Id, ProductName = Lang.PickReq(product.NameUk, product.NameEn, lang, product.NameCs),
                    Qty = 1, UnitPrice = product.Price, LineTotal = product.Price,
                },
            ],
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        var (data, signature) = liqpay.BuildCheckout(order, lang);
        await notifier.NotifyManagersAsync($"Аудит очікує оплати №{order.Number} — {req.Date:dd.MM.yyyy}",
            $"{name}, {phone}{(order.Email is null ? "" : ", " + order.Email)}\nАдреса: {address}\nДата: {req.Date:dd.MM.yyyy}\n" +
            $"{(comment is null ? "" : $"Коментар: {comment}\n")}Сума: {order.Total:N0} грн", ct);
        return Results.Ok(OrdersEndpoints.ToDto(order, new LiqPayCheckoutDto(LiqPayService.CheckoutUrl, data, signature)));
    }

    private static async Task<Product> AuditProductAsync(AppDbContext db, CancellationToken ct) =>
        await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == AuditWindow.ProductSlug && p.Published, ct)
        ?? throw ProblemException.NotFound("Audit product not found");
}
