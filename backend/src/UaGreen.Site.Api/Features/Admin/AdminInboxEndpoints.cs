using Microsoft.EntityFrameworkCore;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Persistence;
using UaGreen.Site.Api.Infrastructure.Storage;

namespace UaGreen.Site.Api.Features.Admin;

public sealed record ApplicationAdminDto(Guid Id, Guid? VacancyId, string? VacancyTitle, string Name, string Phone, string? Email,
    string? Message, bool HasCv, string? CvFileName, bool IsRead, DateTime CreatedAt);
public sealed record ContactAdminDto(Guid Id, string Name, string Phone, string? Email, string Message, bool IsRead, DateTime CreatedAt);
public sealed record ReadUpdate(bool IsRead);

/// Job applications (HR) and contact requests (Sales/Editor).
public static class AdminInboxEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var a = app.MapGroup("/api/admin/applications").RequireAuthorization(Policies.Hr);

        a.MapGet("/", async (bool? unread, AppDbContext db, CancellationToken ct) =>
        {
            var q = from x in db.Applications.AsNoTracking()
                    join v in db.Vacancies.AsNoTracking() on x.VacancyId equals v.Id into vj
                    from v in vj.DefaultIfEmpty()
                    where unread != true || !x.IsRead
                    orderby x.CreatedAt descending
                    select new ApplicationAdminDto(x.Id, x.VacancyId, v != null ? v.TitleUk : null, x.Name, x.Phone, x.Email,
                        x.Message, x.CvKey != null, x.CvFileName, x.IsRead, x.CreatedAt);
            return await q.Take(500).ToListAsync(ct);
        });

        // Short-lived presigned link to the CV in the private bucket.
        a.MapGet("/{id:guid}/cv", async (Guid id, AppDbContext db, FileStorage storage, CancellationToken ct) =>
        {
            var x = await db.Applications.AsNoTracking().FirstOrDefaultAsync(z => z.Id == id, ct) ?? throw ProblemException.NotFound("Application not found");
            if (x.CvKey is null) throw ProblemException.NotFound("No CV attached");
            return Results.Ok(new { url = await storage.PresignPrivateDownloadAsync(x.CvKey, TimeSpan.FromMinutes(5)), fileName = x.CvFileName });
        });

        a.MapPatch("/{id:guid}", async (Guid id, ReadUpdate req, AppDbContext db, CancellationToken ct) =>
        {
            if (await db.Applications.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRead, req.IsRead), ct) == 0)
                throw ProblemException.NotFound("Application not found");
            return Results.NoContent();
        });

        a.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, FileStorage storage, CancellationToken ct) =>
        {
            var x = await db.Applications.FirstOrDefaultAsync(z => z.Id == id, ct) ?? throw ProblemException.NotFound("Application not found");
            db.Applications.Remove(x);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        var c = app.MapGroup("/api/admin/contact-requests").RequireAuthorization(Policies.SalesOrEditor);

        c.MapGet("/", async (bool? unread, AppDbContext db, CancellationToken ct) =>
            await db.ContactRequests.AsNoTracking().Where(x => unread != true || !x.IsRead).OrderByDescending(x => x.CreatedAt).Take(500)
                .Select(x => new ContactAdminDto(x.Id, x.Name, x.Phone, x.Email, x.Message, x.IsRead, x.CreatedAt)).ToListAsync(ct));

        c.MapPatch("/{id:guid}", async (Guid id, ReadUpdate req, AppDbContext db, CancellationToken ct) =>
        {
            if (await db.ContactRequests.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRead, req.IsRead), ct) == 0)
                throw ProblemException.NotFound("Request not found");
            return Results.NoContent();
        });

        c.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            if (await db.ContactRequests.Where(x => x.Id == id).ExecuteDeleteAsync(ct) == 0) throw ProblemException.NotFound("Request not found");
            return Results.NoContent();
        });
    }
}
