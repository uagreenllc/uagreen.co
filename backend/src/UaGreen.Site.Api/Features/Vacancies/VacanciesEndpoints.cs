using Microsoft.EntityFrameworkCore;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Persistence;

namespace UaGreen.Site.Api.Features.Vacancies;

public sealed record VacancyDto(
    string Slug, string Title, string Department, string EmploymentType, string City,
    string? Experience, string? Language, string? RequirementsHtml, string? NiceToHaveHtml, string? ConditionsHtml,
    DateTime PublishedAt);

public static class VacanciesEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/vacancies").AllowAnonymous().CacheOutput("public");

        g.MapGet("/", async (string? lang, AppDbContext db, CancellationToken ct) =>
        {
            var l = Lang.Norm(lang);
            var rows = await db.Vacancies.AsNoTracking().Where(v => v.IsActive)
                .OrderBy(v => v.Sort).ThenByDescending(v => v.PublishedAt).ToListAsync(ct);
            return rows.Select(v => ToDto(v, l));
        });

        g.MapGet("/{slug}", async (string slug, string? lang, AppDbContext db, CancellationToken ct) =>
        {
            var v = await db.Vacancies.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == slug && x.IsActive, ct)
                ?? throw ProblemException.NotFound("Vacancy not found");
            return ToDto(v, Lang.Norm(lang));
        });
    }

    private static VacancyDto ToDto(Vacancy v, string l) => new(
        v.Slug, Lang.PickReq(v.TitleUk, v.TitleEn, l, v.TitleCs), Lang.PickReq(v.Department, null, l, v.DepartmentCs), Lang.PickReq(v.EmploymentType, null, l, v.EmploymentTypeCs),
        Lang.PickReq(v.City, null, l, v.CityCs), Lang.Pick(v.Experience, null, l, v.ExperienceCs), Lang.Pick(v.Language, null, l, v.LanguageCs),
        Lang.Pick(v.RequirementsHtmlUk, v.RequirementsHtmlEn, l, v.RequirementsHtmlCs),
        Lang.Pick(v.NiceToHaveHtmlUk, v.NiceToHaveHtmlEn, l, v.NiceToHaveHtmlCs),
        Lang.Pick(v.ConditionsHtmlUk, v.ConditionsHtmlEn, l, v.ConditionsHtmlCs),
        v.PublishedAt);
}
