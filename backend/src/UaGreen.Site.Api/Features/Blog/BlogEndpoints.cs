using Microsoft.EntityFrameworkCore;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Infrastructure.Persistence;
using UaGreen.Site.Api.Infrastructure.Storage;

namespace UaGreen.Site.Api.Features.Blog;

public sealed record PostCardDto(string Slug, string Title, string? Excerpt, string? Cover, string? Author, DateTime PublishedAt);
public sealed record PostDto(string Slug, string Title, string? Excerpt, string? Cover, string? Author, DateTime PublishedAt,
    string BodyHtml, string? SeoTitle, string? SeoDesc, DateTime UpdatedAt);

public static class BlogEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/blog").AllowAnonymous().CacheOutput("public");

        g.MapGet("/", async (string? lang, int? page, int? pageSize, AppDbContext db, CancellationToken ct) =>
        {
            var l = Lang.Norm(lang);
            var pn = Math.Max(1, page ?? 1);
            var ps = Math.Clamp(pageSize ?? 12, 1, 100);
            var q = db.BlogPosts.AsNoTracking().Where(p => p.Published && p.PublishedAt <= DateTime.UtcNow);
            var total = await q.CountAsync(ct);
            var rows = await q.OrderByDescending(p => p.PublishedAt).Skip((pn - 1) * ps).Take(ps).ToListAsync(ct);
            return new Page<PostCardDto>(rows.Select(p => new PostCardDto(
                p.Slug, Lang.PickReq(p.TitleUk, p.TitleEn, l), Lang.Pick(p.ExcerptUk, p.ExcerptEn, l),
                p.CoverKey is null ? null : FileStorage.Url(p.CoverKey), p.Author, p.PublishedAt)).ToList(), total, pn, ps);
        });

        g.MapGet("/{slug}", async (string slug, string? lang, AppDbContext db, CancellationToken ct) =>
        {
            var l = Lang.Norm(lang);
            var p = await db.BlogPosts.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == slug && x.Published, ct)
                ?? throw ProblemException.NotFound("Post not found");
            return new PostDto(p.Slug, Lang.PickReq(p.TitleUk, p.TitleEn, l), Lang.Pick(p.ExcerptUk, p.ExcerptEn, l),
                p.CoverKey is null ? null : FileStorage.Url(p.CoverKey), p.Author, p.PublishedAt,
                Lang.PickReq(p.BodyHtmlUk, p.BodyHtmlEn, l),
                Lang.Pick(p.SeoTitleUk, p.SeoTitleEn, l), Lang.Pick(p.SeoDescUk, p.SeoDescEn, l), p.UpdatedAt);
        });
    }
}
