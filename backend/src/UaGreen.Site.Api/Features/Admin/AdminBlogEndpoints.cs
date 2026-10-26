using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Persistence;
using UaGreen.Site.Api.Infrastructure.Storage;
using static UaGreen.Site.Api.Features.Admin.AdminCommon;

namespace UaGreen.Site.Api.Features.Admin;

public sealed record BlogUpsert(
    string Slug, string TitleUk, string? TitleEn, string? ExcerptUk, string? ExcerptEn, string BodyHtmlUk, string? BodyHtmlEn,
    string? CoverKey, string? Author, DateTime? PublishedAt, string? SeoTitleUk, string? SeoTitleEn, string? SeoDescUk, string? SeoDescEn, bool Published);

public sealed record BlogAdminDto(
    Guid Id, string Slug, string TitleUk, string? TitleEn, string? ExcerptUk, string? ExcerptEn, string BodyHtmlUk, string? BodyHtmlEn,
    string? CoverKey, string? CoverUrl, string? Author, DateTime PublishedAt, string? SeoTitleUk, string? SeoTitleEn, string? SeoDescUk, string? SeoDescEn,
    bool Published, DateTime UpdatedAt);

public static class AdminBlogEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin/blog").RequireAuthorization(Policies.Editor);

        g.MapGet("/", async (AppDbContext db, CancellationToken ct) =>
            (await db.BlogPosts.AsNoTracking().OrderByDescending(p => p.PublishedAt).ToListAsync(ct)).Select(ToDto));

        g.MapGet("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
            ToDto(await db.BlogPosts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw ProblemException.NotFound("Post not found")));

        g.MapPost("/", async (BlogUpsert req, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
        {
            var p = new BlogPost { Id = Guid.NewGuid() };
            await ApplyAsync(p, req, db, ct);
            db.BlogPosts.Add(p);
            await db.SaveChangesAsync(ct);
            await EvictPublicAsync(cache, ct);
            return Results.Created($"/api/admin/blog/{p.Id}", ToDto(p));
        });

        g.MapPut("/{id:guid}", async (Guid id, BlogUpsert req, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
        {
            var p = await db.BlogPosts.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw ProblemException.NotFound("Post not found");
            await ApplyAsync(p, req, db, ct);
            await db.SaveChangesAsync(ct);
            await EvictPublicAsync(cache, ct);
            return Results.Ok(ToDto(p));
        });

        g.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
        {
            if (await db.BlogPosts.Where(x => x.Id == id).ExecuteDeleteAsync(ct) == 0) throw ProblemException.NotFound("Post not found");
            await EvictPublicAsync(cache, ct);
            return Results.NoContent();
        });
    }

    private static async Task ApplyAsync(BlogPost p, BlogUpsert req, AppDbContext db, CancellationToken ct)
    {
        var slug = Slug.Require(req.Slug);
        if (await db.BlogPosts.AnyAsync(x => x.Slug == slug && x.Id != p.Id, ct)) throw ProblemException.Conflict("Slug already in use");
        if (string.IsNullOrWhiteSpace(req.TitleUk)) throw ProblemException.BadRequest("titleUk is required");
        if (string.IsNullOrWhiteSpace(req.BodyHtmlUk)) throw ProblemException.BadRequest("bodyHtmlUk is required");
        p.Slug = slug;
        p.TitleUk = req.TitleUk.Trim(); p.TitleEn = Clean(req.TitleEn);
        p.ExcerptUk = Clean(req.ExcerptUk); p.ExcerptEn = Clean(req.ExcerptEn);
        p.BodyHtmlUk = req.BodyHtmlUk; p.BodyHtmlEn = Clean(req.BodyHtmlEn);
        p.CoverKey = Clean(req.CoverKey); p.Author = Clean(req.Author);
        p.PublishedAt = req.PublishedAt ?? (p.PublishedAt == default ? DateTime.UtcNow : p.PublishedAt);
        p.SeoTitleUk = Clean(req.SeoTitleUk); p.SeoTitleEn = Clean(req.SeoTitleEn);
        p.SeoDescUk = Clean(req.SeoDescUk); p.SeoDescEn = Clean(req.SeoDescEn);
        p.Published = req.Published;
        p.UpdatedAt = DateTime.UtcNow;
    }

    private static BlogAdminDto ToDto(BlogPost p) => new(
        p.Id, p.Slug, p.TitleUk, p.TitleEn, p.ExcerptUk, p.ExcerptEn, p.BodyHtmlUk, p.BodyHtmlEn,
        p.CoverKey, p.CoverKey is null ? null : FileStorage.Url(p.CoverKey), p.Author, p.PublishedAt,
        p.SeoTitleUk, p.SeoTitleEn, p.SeoDescUk, p.SeoDescEn, p.Published, p.UpdatedAt);
}
