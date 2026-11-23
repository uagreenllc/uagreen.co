using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Persistence;
using static UaGreen.Site.Api.Features.Admin.AdminCommon;

namespace UaGreen.Site.Api.Features.Admin;

public sealed record VacancyUpsert(
    string Slug, string TitleUk, string? TitleEn, string Department, string EmploymentType, string City, string? Experience, string? Language,
    string? RequirementsHtmlUk, string? RequirementsHtmlEn, string? NiceToHaveHtmlUk, string? NiceToHaveHtmlEn,
    string? ConditionsHtmlUk, string? ConditionsHtmlEn, bool IsActive, int Sort, DateTime? PublishedAt);

public sealed record VacancyAdminDto(
    Guid Id, string Slug, string TitleUk, string? TitleEn, string Department, string EmploymentType, string City, string? Experience, string? Language,
    string? RequirementsHtmlUk, string? RequirementsHtmlEn, string? NiceToHaveHtmlUk, string? NiceToHaveHtmlEn,
    string? ConditionsHtmlUk, string? ConditionsHtmlEn, bool IsActive, int Sort, DateTime PublishedAt, DateTime UpdatedAt, int Applications);

public static class AdminVacanciesEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin/vacancies").RequireAuthorization(Policies.Hr);

        // filter: active | inactive
        g.MapGet("/", async (string? q, string? filter, int? page, int? pageSize, AppDbContext db, CancellationToken ct) =>
        {
            var query = db.Vacancies.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(v => EF.Functions.ILike(v.TitleUk, Like(q)) || EF.Functions.ILike(v.Department, Like(q)) || EF.Functions.ILike(v.City, Like(q)));
            if (filter == "active") query = query.Where(v => v.IsActive);
            if (filter == "inactive") query = query.Where(v => !v.IsActive);
            return await PageAsync(
                query.OrderBy(v => v.Sort).ThenByDescending(v => v.PublishedAt)
                    .Select(v => new { v, N = db.Applications.Count(a => a.VacancyId == v.Id) }),
                page, pageSize, x => ToDto(x.v, x.N), ct);
        });

        g.MapGet("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var v = await db.Vacancies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw ProblemException.NotFound("Вакансію не знайдено");
            return ToDto(v, await db.Applications.CountAsync(a => a.VacancyId == id, ct));
        });

        g.MapPost("/", async (VacancyUpsert req, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
        {
            var v = new Vacancy { Id = Guid.NewGuid() };
            await ApplyAsync(v, req, db, ct);
            db.Vacancies.Add(v);
            await db.SaveChangesAsync(ct);
            await EvictPublicAsync(cache, ct);
            return Results.Created($"/api/admin/vacancies/{v.Id}", ToDto(v, 0));
        });

        g.MapPut("/{id:guid}", async (Guid id, VacancyUpsert req, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
        {
            var v = await db.Vacancies.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw ProblemException.NotFound("Вакансію не знайдено");
            var oldSlug = v.Slug;
            await ApplyAsync(v, req, db, ct);
            await RedirectSlugAsync(db, "/vacancies", oldSlug, v.Slug, ct);
            await db.SaveChangesAsync(ct);
            await EvictPublicAsync(cache, ct);
            return Results.Ok(ToDto(v, await db.Applications.CountAsync(a => a.VacancyId == id, ct)));
        });

        g.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
        {
            // Keep the applications; they just lose the vacancy link.
            await db.Applications.Where(a => a.VacancyId == id).ExecuteUpdateAsync(s => s.SetProperty(a => a.VacancyId, (Guid?)null), ct);
            if (await db.Vacancies.Where(x => x.Id == id).ExecuteDeleteAsync(ct) == 0) throw ProblemException.NotFound("Вакансію не знайдено");
            await EvictPublicAsync(cache, ct);
            return Results.NoContent();
        });
    }

    private static async Task ApplyAsync(Vacancy v, VacancyUpsert req, AppDbContext db, CancellationToken ct)
    {
        var slug = Slug.Require(req.Slug);
        if (await db.Vacancies.AnyAsync(x => x.Slug == slug && x.Id != v.Id, ct)) throw ProblemException.Conflict("Такий slug уже має інша вакансія");
        if (string.IsNullOrWhiteSpace(req.TitleUk)) throw ProblemException.BadRequest("Вкажіть назву (UK)");
        v.Slug = slug;
        v.TitleUk = req.TitleUk.Trim(); v.TitleEn = Clean(req.TitleEn);
        v.Department = (req.Department ?? "").Trim(); v.EmploymentType = (req.EmploymentType ?? "").Trim();
        v.City = Clean(req.City) ?? "Львів"; v.Experience = Clean(req.Experience); v.Language = Clean(req.Language);
        v.RequirementsHtmlUk = Clean(req.RequirementsHtmlUk); v.RequirementsHtmlEn = Clean(req.RequirementsHtmlEn);
        v.NiceToHaveHtmlUk = Clean(req.NiceToHaveHtmlUk); v.NiceToHaveHtmlEn = Clean(req.NiceToHaveHtmlEn);
        v.ConditionsHtmlUk = Clean(req.ConditionsHtmlUk); v.ConditionsHtmlEn = Clean(req.ConditionsHtmlEn);
        v.IsActive = req.IsActive; v.Sort = req.Sort;
        v.PublishedAt = req.PublishedAt ?? (v.PublishedAt == default ? DateTime.UtcNow : v.PublishedAt);
        v.UpdatedAt = DateTime.UtcNow;
    }

    private static VacancyAdminDto ToDto(Vacancy v, int applications) => new(
        v.Id, v.Slug, v.TitleUk, v.TitleEn, v.Department, v.EmploymentType, v.City, v.Experience, v.Language,
        v.RequirementsHtmlUk, v.RequirementsHtmlEn, v.NiceToHaveHtmlUk, v.NiceToHaveHtmlEn, v.ConditionsHtmlUk, v.ConditionsHtmlEn,
        v.IsActive, v.Sort, v.PublishedAt, v.UpdatedAt, applications);
}
