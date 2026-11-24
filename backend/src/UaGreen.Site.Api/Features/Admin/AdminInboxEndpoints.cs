using Microsoft.EntityFrameworkCore;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Crm;
using UaGreen.Site.Api.Infrastructure.Persistence;
using UaGreen.Site.Api.Infrastructure.Storage;
using static UaGreen.Site.Api.Features.Admin.AdminCommon;

namespace UaGreen.Site.Api.Features.Admin;

public sealed record ApplicationAdminDto(Guid Id, Guid? VacancyId, string? VacancyTitle, string Name, string Phone, string? Email,
    string? Message, bool HasCv, string? CvFileName, DateTime CreatedAt, string? GospodarError, int GospodarAttempts);
public sealed record ContactAdminDto(Guid Id, string Name, string Phone, string? Email, string Message, bool IsRead, string? Note,
    CrmStateDto Crm, DateTime CreatedAt);
/// Partial update; null = leave as is. Note "" clears it.
public sealed record InboxPatch(bool? IsRead, string? Note, string? Status);

/// Job applications (HR) and contact requests (Sales/Editor).
public static class AdminInboxEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var a = app.MapGroup("/api/admin/applications").RequireAuthorization(Policies.Hr);

        // Only what never reached Gospodar HR: delivered applications are worked there, not here.
        a.MapGet("/", async (string? q, int? page, int? pageSize, AppDbContext db, CancellationToken ct) =>
        {
            var rows = db.Applications.AsNoTracking().Where(x => x.GospodarCandidateId == null);
            if (!string.IsNullOrWhiteSpace(q))
                rows = rows.Where(x => EF.Functions.ILike(x.Name, Like(q)) || x.Phone.Contains(q.Trim()) || (x.Email != null && EF.Functions.ILike(x.Email, Like(q))));
            var query = from x in rows
                        join v in db.Vacancies.AsNoTracking() on x.VacancyId equals v.Id into vj
                        from v in vj.DefaultIfEmpty()
                        orderby x.CreatedAt descending
                        select new { x, Title = v != null ? v.TitleUk : null };
            return await PageAsync(query, page, pageSize, r => ToDto(r.x, r.Title), ct);
        });

        // CVs live outside the public media root; stream them to HR through the API.
        a.MapGet("/{id:guid}/cv", async (Guid id, AppDbContext db, FileStorage storage, CancellationToken ct) =>
        {
            var x = await db.Applications.AsNoTracking().FirstOrDefaultAsync(z => z.Id == id, ct) ?? throw ProblemException.NotFound("Відгук не знайдено");
            if (x.CvKey is null) throw ProblemException.NotFound("CV не додано");
            var stream = storage.OpenPrivate(x.CvKey) ?? throw ProblemException.NotFound("Файл CV зник зі сховища");
            return Results.File(stream, FileStorage.ContentTypeFor(x.CvKey), x.CvFileName ?? "cv" + Path.GetExtension(x.CvKey));
        });

        // Send to Gospodar now, with a fresh set of automatic retries after it.
        a.MapPost("/{id:guid}/resend", async (Guid id, CrmSync sync, AppDbContext db, CancellationToken ct) =>
        {
            var x = await db.Applications.FirstOrDefaultAsync(z => z.Id == id, ct) ?? throw ProblemException.NotFound("Відгук не знайдено");
            var v = x.VacancyId is null ? null : await db.Vacancies.AsNoTracking().FirstOrDefaultAsync(z => z.Id == x.VacancyId, ct);
            x.GospodarAttempts = 0;
            x.GospodarLastAttemptAt = null;
            await sync.PushAsync(x, v, ct);
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToDto(x, v?.TitleUk));
        });

        a.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, FileStorage storage, CancellationToken ct) =>
        {
            var x = await db.Applications.FirstOrDefaultAsync(z => z.Id == id, ct) ?? throw ProblemException.NotFound("Відгук не знайдено");
            db.Applications.Remove(x);
            await db.SaveChangesAsync(ct);
            if (x.CvKey is not null) storage.DeletePrivate(x.CvKey);
            return Results.NoContent();
        });

        var c = app.MapGroup("/api/admin/contact-requests").RequireAuthorization(Policies.SalesOrEditor);

        c.MapGet("/", async (bool? unread, string? q, string? crm, int? page, int? pageSize, AppDbContext db, CancellationToken ct) =>
        {
            var rows = db.ContactRequests.AsNoTracking();
            if (unread == true) rows = rows.Where(x => !x.IsRead);
            if (crm == "failed") rows = rows.Where(x => x.CrmDealId == null && x.CrmError != null);
            if (!string.IsNullOrWhiteSpace(q))
                rows = rows.Where(x => EF.Functions.ILike(x.Name, Like(q)) || x.Phone.Contains(q.Trim())
                    || (x.Email != null && EF.Functions.ILike(x.Email, Like(q))) || EF.Functions.ILike(x.Message, Like(q)));
            return await PageAsync(rows.OrderByDescending(x => x.CreatedAt), page, pageSize, ToDto, ct);
        });

        c.MapPatch("/{id:guid}", async (Guid id, InboxPatch req, AppDbContext db, CancellationToken ct) =>
        {
            var x = await db.ContactRequests.FirstOrDefaultAsync(z => z.Id == id, ct) ?? throw ProblemException.NotFound("Запит не знайдено");
            x.IsRead = req.IsRead ?? x.IsRead;
            if (req.Note is not null) x.Note = Clean(req.Note);
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToDto(x));
        });

        c.MapPost("/{id:guid}/crm", async (Guid id, CrmSync sync, AppDbContext db, CancellationToken ct) =>
        {
            var x = await db.ContactRequests.FirstOrDefaultAsync(z => z.Id == id, ct) ?? throw ProblemException.NotFound("Запит не знайдено");
            await sync.PushAsync(x, ct);
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToDto(x));
        });

        c.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            if (await db.ContactRequests.Where(x => x.Id == id).ExecuteDeleteAsync(ct) == 0) throw ProblemException.NotFound("Запит не знайдено");
            return Results.NoContent();
        });
    }

    private static ApplicationAdminDto ToDto(Application x, string? vacancyTitle) => new(x.Id, x.VacancyId, vacancyTitle, x.Name, x.Phone, x.Email,
        x.Message, x.CvKey != null, x.CvFileName, x.CreatedAt, x.GospodarError, x.GospodarAttempts);

    private static ContactAdminDto ToDto(ContactRequest x) => new(x.Id, x.Name, x.Phone, x.Email, x.Message, x.IsRead, x.Note,
        new CrmStateDto(x.CrmDealId, x.CrmSyncedAt, x.CrmError, x.CrmAttempts), x.CreatedAt);
}
