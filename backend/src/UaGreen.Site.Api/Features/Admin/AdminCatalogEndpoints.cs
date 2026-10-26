using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Persistence;
using UaGreen.Site.Api.Infrastructure.Storage;
using static UaGreen.Site.Api.Features.Admin.AdminCommon;

namespace UaGreen.Site.Api.Features.Admin;

public sealed record CategoryUpsert(string Slug, string NameUk, string? NameEn, int Sort, string? ImageKey);
public sealed record CategoryAdminDto(Guid Id, string Slug, string NameUk, string? NameEn, int Sort, string? ImageKey, string? ImageUrl, int ProductCount);

public sealed record ImageUpsert(string Key, string? Alt, int Sort);
public sealed record FileUpsert(string Key, string Title);
public sealed record OptionValueUpsert(Guid? Id, string ValueUk, string? ValueEn, decimal PriceDelta, int Sort);
public sealed record OptionUpsert(Guid? Id, string NameUk, string? NameEn, int Sort, List<OptionValueUpsert> Values);
public sealed record ProductUpsert(
    string Slug, string? Sku, Guid? CategoryId,
    string NameUk, string? NameEn, string? ShortDescUk, string? ShortDescEn,
    string? DescriptionHtmlUk, string? DescriptionHtmlEn, string? SpecsHtmlUk, string? SpecsHtmlEn, string? FaqHtmlUk, string? FaqHtmlEn,
    decimal Price, decimal? OldPrice, bool InStock, bool IsFeatured, int Sort,
    string? SeoTitleUk, string? SeoTitleEn, string? SeoDescUk, string? SeoDescEn, bool Published,
    List<ImageUpsert>? Images, List<FileUpsert>? Files, List<OptionUpsert>? Options);

public sealed record ProductAdminListDto(Guid Id, string Slug, string? Sku, string NameUk, decimal Price, decimal? OldPrice,
    bool InStock, bool IsFeatured, bool Published, int Sort, Guid? CategoryId, string? CategoryName, string? Image, DateTime UpdatedAt);

public sealed record ImageAdminDto(Guid Id, string Key, string Url, string? Alt, int Sort);
public sealed record FileAdminDto(Guid Id, string Key, string Url, string Title);
public sealed record OptionValueAdminDto(Guid Id, string ValueUk, string? ValueEn, decimal PriceDelta, int Sort);
public sealed record OptionAdminDto(Guid Id, string NameUk, string? NameEn, int Sort, List<OptionValueAdminDto> Values);
public sealed record ProductAdminDto(
    Guid Id, string Slug, string? Sku, Guid? CategoryId,
    string NameUk, string? NameEn, string? ShortDescUk, string? ShortDescEn,
    string? DescriptionHtmlUk, string? DescriptionHtmlEn, string? SpecsHtmlUk, string? SpecsHtmlEn, string? FaqHtmlUk, string? FaqHtmlEn,
    decimal Price, decimal? OldPrice, bool InStock, bool IsFeatured, int Sort,
    string? SeoTitleUk, string? SeoTitleEn, string? SeoDescUk, string? SeoDescEn, bool Published,
    List<ImageAdminDto> Images, List<FileAdminDto> Files, List<OptionAdminDto> Options, DateTime CreatedAt, DateTime UpdatedAt);

public static class AdminCatalogEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin/catalog").RequireAuthorization(Policies.Sales);

        // ── Categories ──
        g.MapGet("/categories", async (AppDbContext db, CancellationToken ct) =>
            (await db.Categories.AsNoTracking().OrderBy(c => c.Sort).ThenBy(c => c.NameUk)
                .Select(c => new { c, Count = c.Products.Count }).ToListAsync(ct))
            .Select(x => ToDto(x.c, x.Count)));

        g.MapPost("/categories", async (CategoryUpsert req, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
        {
            var c = new Category { Id = Guid.NewGuid() };
            await ApplyAsync(c, req, db, ct);
            db.Categories.Add(c);
            await db.SaveChangesAsync(ct);
            await EvictPublicAsync(cache, ct);
            return Results.Created($"/api/admin/catalog/categories/{c.Id}", ToDto(c, 0));
        });

        g.MapPut("/categories/{id:guid}", async (Guid id, CategoryUpsert req, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
        {
            var c = await db.Categories.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw ProblemException.NotFound("Category not found");
            await ApplyAsync(c, req, db, ct);
            await db.SaveChangesAsync(ct);
            await EvictPublicAsync(cache, ct);
            return Results.Ok(ToDto(c, await db.Products.CountAsync(p => p.CategoryId == id, ct)));
        });

        g.MapDelete("/categories/{id:guid}", async (Guid id, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
        {
            var n = await db.Categories.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
            if (n == 0) throw ProblemException.NotFound("Category not found");
            await EvictPublicAsync(cache, ct);
            return Results.NoContent();
        });

        // ── Products ──
        g.MapGet("/products", async (Guid? categoryId, string? q, AppDbContext db, CancellationToken ct) =>
        {
            var query = db.Products.AsNoTracking().Include(p => p.Category).Include(p => p.Images).AsQueryable();
            if (categoryId is not null) query = query.Where(p => p.CategoryId == categoryId);
            if (!string.IsNullOrWhiteSpace(q)) query = query.Where(p => p.NameUk.Contains(q) || (p.Sku != null && p.Sku.Contains(q)));
            var rows = await query.OrderBy(p => p.Sort).ThenBy(p => p.NameUk).ToListAsync(ct);
            return rows.Select(p => new ProductAdminListDto(p.Id, p.Slug, p.Sku, p.NameUk, p.Price, p.OldPrice, p.InStock, p.IsFeatured,
                p.Published, p.Sort, p.CategoryId, p.Category?.NameUk,
                p.Images.OrderBy(i => i.Sort).Select(i => FileStorage.Url(i.Key)).FirstOrDefault(), p.UpdatedAt));
        });

        g.MapGet("/products/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
            ToDto(await LoadAsync(db, id, ct) ?? throw ProblemException.NotFound("Product not found")));

        g.MapPost("/products", async (ProductUpsert req, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
        {
            var p = new Product { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
            await ApplyAsync(p, req, db, ct);
            db.Products.Add(p);
            await db.SaveChangesAsync(ct);
            await EvictPublicAsync(cache, ct);
            return Results.Created($"/api/admin/catalog/products/{p.Id}", ToDto((await LoadAsync(db, p.Id, ct))!));
        });

        g.MapPut("/products/{id:guid}", async (Guid id, ProductUpsert req, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
        {
            var p = await LoadAsync(db, id, ct, track: true) ?? throw ProblemException.NotFound("Product not found");
            await ApplyAsync(p, req, db, ct);
            await db.SaveChangesAsync(ct);
            await EvictPublicAsync(cache, ct);
            return Results.Ok(ToDto((await LoadAsync(db, id, ct))!));
        });

        g.MapDelete("/products/{id:guid}", async (Guid id, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
        {
            var n = await db.Products.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
            if (n == 0) throw ProblemException.NotFound("Product not found");
            await EvictPublicAsync(cache, ct);
            return Results.NoContent();
        });
    }

    private static Task<Product?> LoadAsync(AppDbContext db, Guid id, CancellationToken ct, bool track = false)
    {
        var q = db.Products.Include(p => p.Images).Include(p => p.Files).Include(p => p.Options).ThenInclude(o => o.Values).AsQueryable();
        if (!track) q = q.AsNoTracking();
        return q.FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    private static async Task ApplyAsync(Category c, CategoryUpsert req, AppDbContext db, CancellationToken ct)
    {
        var slug = Slug.Require(req.Slug);
        if (await db.Categories.AnyAsync(x => x.Slug == slug && x.Id != c.Id, ct)) throw ProblemException.Conflict("Slug already in use");
        if (string.IsNullOrWhiteSpace(req.NameUk)) throw ProblemException.BadRequest("nameUk is required");
        c.Slug = slug;
        c.NameUk = req.NameUk.Trim();
        c.NameEn = Clean(req.NameEn);
        c.Sort = req.Sort;
        c.ImageKey = Clean(req.ImageKey);
    }

    private static async Task ApplyAsync(Product p, ProductUpsert req, AppDbContext db, CancellationToken ct)
    {
        var slug = Slug.Require(req.Slug);
        if (await db.Products.AnyAsync(x => x.Slug == slug && x.Id != p.Id, ct)) throw ProblemException.Conflict("Slug already in use");
        if (string.IsNullOrWhiteSpace(req.NameUk)) throw ProblemException.BadRequest("nameUk is required");
        if (req.Price < 0 || req.OldPrice < 0) throw ProblemException.BadRequest("Price must be non-negative");
        if (req.CategoryId is not null && !await db.Categories.AnyAsync(c => c.Id == req.CategoryId, ct))
            throw ProblemException.BadRequest("Category not found");

        p.Slug = slug; p.Sku = Clean(req.Sku); p.CategoryId = req.CategoryId;
        p.NameUk = req.NameUk.Trim(); p.NameEn = Clean(req.NameEn);
        p.ShortDescUk = Clean(req.ShortDescUk); p.ShortDescEn = Clean(req.ShortDescEn);
        p.DescriptionHtmlUk = Clean(req.DescriptionHtmlUk); p.DescriptionHtmlEn = Clean(req.DescriptionHtmlEn);
        p.SpecsHtmlUk = Clean(req.SpecsHtmlUk); p.SpecsHtmlEn = Clean(req.SpecsHtmlEn);
        p.FaqHtmlUk = Clean(req.FaqHtmlUk); p.FaqHtmlEn = Clean(req.FaqHtmlEn);
        p.Price = req.Price; p.OldPrice = req.OldPrice; p.InStock = req.InStock; p.IsFeatured = req.IsFeatured; p.Sort = req.Sort;
        p.SeoTitleUk = Clean(req.SeoTitleUk); p.SeoTitleEn = Clean(req.SeoTitleEn);
        p.SeoDescUk = Clean(req.SeoDescUk); p.SeoDescEn = Clean(req.SeoDescEn);
        p.Published = req.Published;
        p.UpdatedAt = DateTime.UtcNow;

        // Children are replaced wholesale — the admin sends the full lists. Option/value
        // ids are regenerated; carts hold value ids only until checkout re-validates them.
        db.ReplaceProductChildren(p,
            (req.Images ?? []).Select((i, n) => new ProductImage { Id = Guid.NewGuid(), ProductId = p.Id, Key = i.Key.Trim(), Alt = Clean(i.Alt), Sort = i.Sort == 0 ? n : i.Sort }),
            (req.Files ?? []).Select(f => new ProductFile { Id = Guid.NewGuid(), ProductId = p.Id, Key = f.Key.Trim(), Title = f.Title.Trim() }),
            (req.Options ?? []).Select((o, n) =>
            {
                var opt = new ProductOption { Id = Guid.NewGuid(), ProductId = p.Id, NameUk = o.NameUk.Trim(), NameEn = Clean(o.NameEn), Sort = o.Sort == 0 ? n : o.Sort };
                opt.Values.AddRange((o.Values ?? []).Select((v, m) => new ProductOptionValue
                {
                    Id = Guid.NewGuid(), OptionId = opt.Id, ValueUk = v.ValueUk.Trim(), ValueEn = Clean(v.ValueEn), PriceDelta = v.PriceDelta, Sort = v.Sort == 0 ? m : v.Sort,
                }));
                return opt;
            }));
    }

    private static CategoryAdminDto ToDto(Category c, int count)
        => new(c.Id, c.Slug, c.NameUk, c.NameEn, c.Sort, c.ImageKey, c.ImageKey is null ? null : FileStorage.Url(c.ImageKey), count);

    public static ProductAdminDto ToDto(Product p) => new(
        p.Id, p.Slug, p.Sku, p.CategoryId, p.NameUk, p.NameEn, p.ShortDescUk, p.ShortDescEn,
        p.DescriptionHtmlUk, p.DescriptionHtmlEn, p.SpecsHtmlUk, p.SpecsHtmlEn, p.FaqHtmlUk, p.FaqHtmlEn,
        p.Price, p.OldPrice, p.InStock, p.IsFeatured, p.Sort, p.SeoTitleUk, p.SeoTitleEn, p.SeoDescUk, p.SeoDescEn, p.Published,
        p.Images.OrderBy(i => i.Sort).Select(i => new ImageAdminDto(i.Id, i.Key, FileStorage.Url(i.Key), i.Alt, i.Sort)).ToList(),
        p.Files.Select(f => new FileAdminDto(f.Id, f.Key, FileStorage.Url(f.Key), f.Title)).ToList(),
        p.Options.OrderBy(o => o.Sort).Select(o => new OptionAdminDto(o.Id, o.NameUk, o.NameEn, o.Sort,
            o.Values.OrderBy(v => v.Sort).Select(v => new OptionValueAdminDto(v.Id, v.ValueUk, v.ValueEn, v.PriceDelta, v.Sort)).ToList())).ToList(),
        p.CreatedAt, p.UpdatedAt);
}
