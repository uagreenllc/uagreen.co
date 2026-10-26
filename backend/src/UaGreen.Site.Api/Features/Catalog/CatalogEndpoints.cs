using Microsoft.EntityFrameworkCore;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Persistence;
using UaGreen.Site.Api.Infrastructure.Storage;

namespace UaGreen.Site.Api.Features.Catalog;

public sealed record CategoryDto(string Slug, string Name, string? Image, int ProductCount);
public sealed record ImageDto(string Url, string? Alt);
public sealed record FileDto(string Url, string Title);
public sealed record OptionValueDto(Guid Id, string Value, decimal PriceDelta);
public sealed record OptionDto(Guid Id, string Name, IReadOnlyList<OptionValueDto> Values);
public sealed record ProductCardDto(
    string Slug, string? Sku, string Name, string? ShortDesc, decimal Price, decimal? OldPrice,
    bool InStock, bool IsFeatured, string? Image, string? CategorySlug, string? CategoryName);
public sealed record ProductDto(
    string Slug, string? Sku, string Name, string? ShortDesc, decimal Price, decimal? OldPrice,
    bool InStock, bool IsFeatured, string? CategorySlug, string? CategoryName,
    string? DescriptionHtml, string? SpecsHtml, string? FaqHtml,
    IReadOnlyList<ImageDto> Images, IReadOnlyList<FileDto> Files, IReadOnlyList<OptionDto> Options,
    string? SeoTitle, string? SeoDesc, Guid Id);

public static class CatalogEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/catalog").AllowAnonymous().CacheOutput("public");

        g.MapGet("/categories", async (string? lang, AppDbContext db, CancellationToken ct) =>
        {
            var l = Lang.Norm(lang);
            var rows = await db.Categories.AsNoTracking()
                .OrderBy(c => c.Sort).ThenBy(c => c.NameUk)
                .Select(c => new { c.Slug, c.NameUk, c.NameEn, c.ImageKey, Count = c.Products.Count(p => p.Published) })
                .ToListAsync(ct);
            return rows.Select(c => new CategoryDto(c.Slug, Lang.PickReq(c.NameUk, c.NameEn, l),
                c.ImageKey is null ? null : FileStorage.Url(c.ImageKey), c.Count));
        });

        g.MapGet("/products", async (string? lang, string? category, bool? featured, AppDbContext db, CancellationToken ct) =>
        {
            var l = Lang.Norm(lang);
            var q = db.Products.AsNoTracking().Include(p => p.Category).Include(p => p.Images).Where(p => p.Published);
            if (!string.IsNullOrEmpty(category)) q = q.Where(p => p.Category != null && p.Category.Slug == category);
            if (featured == true) q = q.Where(p => p.IsFeatured);
            var rows = await q.OrderBy(p => p.Sort).ThenBy(p => p.NameUk).ToListAsync(ct);
            return rows.Select(p => ToCard(p, l));
        });

        g.MapGet("/products/{slug}", async (string slug, string? lang, AppDbContext db, CancellationToken ct) =>
        {
            var l = Lang.Norm(lang);
            var p = await db.Products.AsNoTracking()
                .Include(x => x.Category).Include(x => x.Images).Include(x => x.Files)
                .Include(x => x.Options).ThenInclude(o => o.Values)
                .FirstOrDefaultAsync(x => x.Slug == slug && x.Published, ct)
                ?? throw ProblemException.NotFound("Product not found");
            return new ProductDto(
                p.Slug, p.Sku, Lang.PickReq(p.NameUk, p.NameEn, l), Lang.Pick(p.ShortDescUk, p.ShortDescEn, l),
                p.Price, p.OldPrice, p.InStock, p.IsFeatured, p.Category?.Slug,
                p.Category is null ? null : Lang.PickReq(p.Category.NameUk, p.Category.NameEn, l),
                Lang.Pick(p.DescriptionHtmlUk, p.DescriptionHtmlEn, l),
                Lang.Pick(p.SpecsHtmlUk, p.SpecsHtmlEn, l),
                Lang.Pick(p.FaqHtmlUk, p.FaqHtmlEn, l),
                p.Images.OrderBy(i => i.Sort).Select(i => new ImageDto(FileStorage.Url(i.Key), i.Alt)).ToList(),
                p.Files.Select(f => new FileDto(FileStorage.Url(f.Key), f.Title)).ToList(),
                p.Options.OrderBy(o => o.Sort).Select(o => new OptionDto(o.Id, Lang.PickReq(o.NameUk, o.NameEn, l),
                    o.Values.OrderBy(v => v.Sort).Select(v => new OptionValueDto(v.Id, Lang.PickReq(v.ValueUk, v.ValueEn, l), v.PriceDelta)).ToList())).ToList(),
                Lang.Pick(p.SeoTitleUk, p.SeoTitleEn, l), Lang.Pick(p.SeoDescUk, p.SeoDescEn, l), p.Id);
        });
    }

    public static ProductCardDto ToCard(Product p, string l) => new(
        p.Slug, p.Sku, Lang.PickReq(p.NameUk, p.NameEn, l), Lang.Pick(p.ShortDescUk, p.ShortDescEn, l),
        p.Price, p.OldPrice, p.InStock, p.IsFeatured,
        p.Images.OrderBy(i => i.Sort).Select(i => FileStorage.Url(i.Key)).FirstOrDefault(),
        p.Category?.Slug, p.Category is null ? null : Lang.PickReq(p.Category.NameUk, p.Category.NameEn, l));
}
