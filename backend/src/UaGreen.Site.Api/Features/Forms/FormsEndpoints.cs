using Microsoft.EntityFrameworkCore;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Email;
using UaGreen.Site.Api.Infrastructure.Persistence;
using UaGreen.Site.Api.Infrastructure.RateLimiting;
using UaGreen.Site.Api.Infrastructure.Storage;

namespace UaGreen.Site.Api.Features.Forms;

public sealed record ContactRequestDto(string Name, string Phone, string? Email, string Message, string? Website);

/// Job applications (multipart, optional CV) and the contact form. Both are
/// honeypot-guarded (`website` field) and rate-limited per IP.
public static class FormsEndpoints
{
    private const long MaxCvBytes = 10L * 1024 * 1024;

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/applications", async (HttpRequest request, AppDbContext db, FileStorage storage, Notifier notifier, CancellationToken ct) =>
        {
            var form = await request.ReadFormAsync(ct);
            if (!string.IsNullOrEmpty(form["website"])) return Results.Ok(new { ok = true });

            var name = form["name"].ToString().Trim();
            var phone = form["phone"].ToString().Trim();
            if (name.Length is < 2 or > 120) throw ProblemException.BadRequest("Name is required");
            if (phone.Length is < 7 or > 30) throw ProblemException.BadRequest("Phone is required");

            Vacancy? vacancy = null;
            var slug = form["vacancySlug"].ToString();
            if (!string.IsNullOrEmpty(slug))
                vacancy = await db.Vacancies.AsNoTracking().FirstOrDefaultAsync(v => v.Slug == slug, ct);

            var a = new Application
            {
                Id = Guid.NewGuid(),
                VacancyId = vacancy?.Id,
                Name = name,
                Phone = phone,
                Email = Trimmed(form["email"]),
                Message = Trimmed(form["message"], 4000),
                CreatedAt = DateTime.UtcNow,
            };

            var cv = form.Files.GetFile("cv");
            if (cv is { Length: > 0 })
            {
                if (cv.Length > MaxCvBytes) throw ProblemException.BadRequest("CV must be under 10 MB");
                if (!FileStorage.DocumentTypes.Contains(cv.ContentType)) throw ProblemException.BadRequest("CV must be PDF or Word");
                a.CvKey = FileStorage.NewKey("cv", cv.ContentType);
                a.CvFileName = Path.GetFileName(cv.FileName);
                await using var s = cv.OpenReadStream();
                await storage.PutPrivateAsync(a.CvKey, s, cv.ContentType, ct);
            }

            db.Applications.Add(a);
            await db.SaveChangesAsync(ct);

            await notifier.NotifyManagersAsync(
                $"Відгук на вакансію: {vacancy?.TitleUk ?? "без вакансії"} — {a.Name}",
                $"{a.Name}, {a.Phone}{(a.Email is null ? "" : ", " + a.Email)}\n\n{a.Message}\n\nCV: {(a.CvKey is null ? "немає" : a.CvFileName)}", ct);
            return Results.Ok(new { ok = true });
        }).AllowAnonymous().DisableAntiforgery().RequireRateLimiting(RateLimitingExtensions.Forms);

        app.MapPost("/api/contact", async (ContactRequestDto req, AppDbContext db, Notifier notifier, CancellationToken ct) =>
        {
            if (!string.IsNullOrEmpty(req.Website)) return Results.Ok(new { ok = true });
            var name = (req.Name ?? "").Trim();
            var phone = (req.Phone ?? "").Trim();
            var message = (req.Message ?? "").Trim();
            if (name.Length is < 2 or > 120) throw ProblemException.BadRequest("Name is required");
            if (phone.Length is < 7 or > 30) throw ProblemException.BadRequest("Phone is required");
            if (message.Length is < 1 or > 4000) throw ProblemException.BadRequest("Message is required");

            var c = new ContactRequest
            {
                Id = Guid.NewGuid(),
                Name = name,
                Phone = phone,
                Email = Trimmed(req.Email),
                Message = message,
                CreatedAt = DateTime.UtcNow,
            };
            db.ContactRequests.Add(c);
            await db.SaveChangesAsync(ct);
            await notifier.NotifyManagersAsync($"Запит з сайту: {c.Name}",
                $"{c.Name}, {c.Phone}{(c.Email is null ? "" : ", " + c.Email)}\n\n{c.Message}", ct);
            return Results.Ok(new { ok = true });
        }).AllowAnonymous().RequireRateLimiting(RateLimitingExtensions.Forms);
    }

    private static string? Trimmed(string? s, int max = 200)
    {
        var t = s?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        return t.Length > max ? t[..max] : t;
    }
}
