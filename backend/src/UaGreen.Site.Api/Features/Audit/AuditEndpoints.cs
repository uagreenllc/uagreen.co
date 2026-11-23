using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Email;
using UaGreen.Site.Api.Infrastructure.Persistence;
using UaGreen.Site.Api.Infrastructure.RateLimiting;

namespace UaGreen.Site.Api.Features.Audit;

/// `Website` is a honeypot: humans never see the field, bots fill it.
public sealed record AuditConsultationDto(string Phone, string? Website);

/// The site-audit flow at the end of the K5 story: a phone-only consultation request,
/// and (see AuditBooking) a paid audit booked into Gospodar.
public static class AuditEndpoints
{
    public const string ConsultationText = "Консультація щодо аудиту ділянки";
    public const string ProductName = "Аудит вітру на ділянці";

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/audit/consultation", async (
            AuditConsultationDto req, AppDbContext db, GospodarClient gospodar, Notifier notifier, CancellationToken ct) =>
        {
            if (!string.IsNullOrEmpty(req.Website)) return Results.Ok(new { ok = true });
            var phone = (req.Phone ?? "").Trim();
            if (phone.Length is < 7 or > 30) throw ProblemException.BadRequest("Phone is required");

            var c = new ContactRequest
            {
                Id = Guid.NewGuid(),
                Phone = phone,
                Message = ConsultationText,
                CreatedAt = DateTime.UtcNow,
            };
            db.ContactRequests.Add(c);
            await db.SaveChangesAsync(ct);

            var pushed = await gospodar.PushLeadAsync(
                new GospodarLead(c.Id.ToString(), null, phone, null, ConsultationText, ProductName), ct);
            await notifier.NotifyManagersAsync($"Консультація щодо аудиту: {phone}",
                $"{phone}\n\n{ConsultationText}\n\nУ Господар передано: {(pushed ? "так" : "ні")}", ct);
            return Results.Ok(new { ok = true });
        }).AllowAnonymous().RequireRateLimiting(RateLimitingExtensions.Forms);
    }
}
