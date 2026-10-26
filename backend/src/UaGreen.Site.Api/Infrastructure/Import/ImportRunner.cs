using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Persistence;
using UaGreen.Site.Api.Infrastructure.Storage;

namespace UaGreen.Site.Api.Infrastructure.Import;

// ── Export contract written by tools/scrape_weblium.py (snake_case JSON) ──
public sealed record ImpCategory(string Slug, string NameUk, string? NameEn, int Sort, string? Image);
public sealed record ImpImage(string File, string? Alt);
public sealed record ImpFile(string File, string Title);
public sealed record ImpOptionValue(string ValueUk, string? ValueEn, decimal PriceDelta);
public sealed record ImpOption(string NameUk, string? NameEn, List<ImpOptionValue> Values);
public sealed record ImpProduct(
    string Slug, string? Sku, string? CategorySlug, string NameUk, string? NameEn, string? ShortDescUk, string? ShortDescEn,
    string? DescriptionHtmlUk, string? DescriptionHtmlEn, string? SpecsHtmlUk, string? SpecsHtmlEn, string? FaqHtmlUk, string? FaqHtmlEn,
    decimal Price, decimal? OldPrice, bool? InStock, bool? IsFeatured, int? Sort,
    string? SeoTitleUk, string? SeoTitleEn, string? SeoDescUk, string? SeoDescEn,
    List<ImpImage>? Images, List<ImpFile>? Files, List<ImpOption>? Options);
public sealed record ImpPost(string Slug, string TitleUk, string? ExcerptUk, string BodyHtmlUk, string? Cover, string? Author,
    DateTime? PublishedAt, string? SeoTitle, string? SeoDesc);
public sealed record ImpVacancy(string Slug, string TitleUk, string? TitleEn, string? Department, string? EmploymentType, string? City,
    string? Experience, string? Language, string? RequirementsHtmlUk, string? RequirementsHtmlEn, string? NiceToHaveHtmlUk, string? NiceToHaveHtmlEn,
    string? ConditionsHtmlUk, string? ConditionsHtmlEn, bool? IsActive);
public sealed record ImpContent(string Key, string Lang, string? Title, string BodyHtml);
public sealed record ImpRedirect(string From, string To);

/// Loads the Weblium export (`Import:Dir`) once at startup: upserts by slug/key and
/// uploads `media/*` to MinIO under the same file name, so `/media/<file>` links
/// embedded in the HTML bodies keep working. Modelled on TTApp's SeedRunner.
public sealed class ImportRunner(IServiceProvider sp, IConfiguration cfg, ILogger<ImportRunner> log) : IHostedService
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public async Task StartAsync(CancellationToken ct)
    {
        var dir = cfg["Import:Dir"];
        if (string.IsNullOrWhiteSpace(dir)) return;
        if (!Directory.Exists(dir)) { log.LogWarning("Import: directory {Dir} not found, skipping", dir); return; }
        try
        {
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var storage = scope.ServiceProvider.GetRequiredService<FileStorage>();
            await ImportAsync(db, dir, storage.PutAsync, storage.ExistsAsync, log, ct);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Import failed");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    public delegate Task PutObject(string key, Stream content, string contentType, CancellationToken ct);
    public delegate Task<bool> ObjectExists(string key, CancellationToken ct);

    public static async Task ImportAsync(AppDbContext db, string dir, PutObject put, ObjectExists exists, ILogger log, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var categories = Read<List<ImpCategory>>(dir, "categories.json") ?? [];
        foreach (var c in categories)
        {
            var row = await db.Categories.FirstOrDefaultAsync(x => x.Slug == c.Slug, ct);
            if (row is null) db.Categories.Add(row = new Category { Id = Guid.NewGuid(), Slug = c.Slug });
            row.NameUk = c.NameUk; row.NameEn = c.NameEn; row.Sort = c.Sort; row.ImageKey = c.Image;
        }
        await db.SaveChangesAsync(ct);
        var catBySlug = await db.Categories.ToDictionaryAsync(c => c.Slug, ct);

        var products = Read<List<ImpProduct>>(dir, "products.json") ?? [];
        foreach (var p in products)
        {
            var row = await db.Products.Include(x => x.Images).Include(x => x.Files).Include(x => x.Options).ThenInclude(o => o.Values)
                .FirstOrDefaultAsync(x => x.Slug == p.Slug, ct);
            if (row is null) db.Products.Add(row = new Product { Id = Guid.NewGuid(), Slug = p.Slug, CreatedAt = now });
            row.Sku = p.Sku;
            row.CategoryId = p.CategorySlug is not null && catBySlug.TryGetValue(p.CategorySlug, out var cat) ? cat.Id : null;
            row.NameUk = p.NameUk; row.NameEn = p.NameEn;
            row.ShortDescUk = p.ShortDescUk; row.ShortDescEn = p.ShortDescEn;
            row.DescriptionHtmlUk = p.DescriptionHtmlUk; row.DescriptionHtmlEn = p.DescriptionHtmlEn;
            row.SpecsHtmlUk = p.SpecsHtmlUk; row.SpecsHtmlEn = p.SpecsHtmlEn;
            row.FaqHtmlUk = p.FaqHtmlUk; row.FaqHtmlEn = p.FaqHtmlEn;
            row.Price = p.Price; row.OldPrice = p.OldPrice;
            row.InStock = p.InStock ?? true; row.IsFeatured = p.IsFeatured ?? false; row.Sort = p.Sort ?? 0;
            row.SeoTitleUk = p.SeoTitleUk; row.SeoTitleEn = p.SeoTitleEn; row.SeoDescUk = p.SeoDescUk; row.SeoDescEn = p.SeoDescEn;
            row.Published = true; row.UpdatedAt = now;
            db.ReplaceProductChildren(row,
                (p.Images ?? []).Select((i, n) => new ProductImage { Id = Guid.NewGuid(), ProductId = row.Id, Key = i.File, Alt = i.Alt, Sort = n }),
                (p.Files ?? []).Select(f => new ProductFile { Id = Guid.NewGuid(), ProductId = row.Id, Key = f.File, Title = f.Title }),
                (p.Options ?? []).Select((o, n) =>
                {
                    var opt = new ProductOption { Id = Guid.NewGuid(), ProductId = row.Id, NameUk = o.NameUk, NameEn = o.NameEn, Sort = n };
                    opt.Values.AddRange((o.Values ?? []).Select((v, m) => new ProductOptionValue
                        { Id = Guid.NewGuid(), OptionId = opt.Id, ValueUk = v.ValueUk, ValueEn = v.ValueEn, PriceDelta = v.PriceDelta, Sort = m }));
                    return opt;
                }));
        }
        await db.SaveChangesAsync(ct);

        var posts = Read<List<ImpPost>>(dir, "blog_posts.json") ?? [];
        foreach (var p in posts)
        {
            var row = await db.BlogPosts.FirstOrDefaultAsync(x => x.Slug == p.Slug, ct);
            if (row is null) db.BlogPosts.Add(row = new BlogPost { Id = Guid.NewGuid(), Slug = p.Slug });
            row.TitleUk = p.TitleUk; row.ExcerptUk = p.ExcerptUk; row.BodyHtmlUk = p.BodyHtmlUk; row.CoverKey = p.Cover; row.Author = p.Author;
            row.PublishedAt = DateTime.SpecifyKind(p.PublishedAt ?? now, DateTimeKind.Utc);
            row.SeoTitleUk = p.SeoTitle; row.SeoDescUk = p.SeoDesc; row.Published = true; row.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);

        var vacancies = Read<List<ImpVacancy>>(dir, "vacancies.json") ?? [];
        var sort = 0;
        foreach (var v in vacancies)
        {
            var row = await db.Vacancies.FirstOrDefaultAsync(x => x.Slug == v.Slug, ct);
            if (row is null) db.Vacancies.Add(row = new Vacancy { Id = Guid.NewGuid(), Slug = v.Slug, PublishedAt = now });
            row.TitleUk = v.TitleUk; row.TitleEn = v.TitleEn; row.Department = v.Department ?? ""; row.EmploymentType = v.EmploymentType ?? "";
            row.City = v.City ?? "Львів"; row.Experience = v.Experience; row.Language = v.Language;
            row.RequirementsHtmlUk = v.RequirementsHtmlUk; row.RequirementsHtmlEn = v.RequirementsHtmlEn;
            row.NiceToHaveHtmlUk = v.NiceToHaveHtmlUk; row.NiceToHaveHtmlEn = v.NiceToHaveHtmlEn;
            row.ConditionsHtmlUk = v.ConditionsHtmlUk; row.ConditionsHtmlEn = v.ConditionsHtmlEn;
            row.IsActive = v.IsActive ?? true; row.Sort = sort++; row.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);

        var content = Read<List<ImpContent>>(dir, "content.json") ?? [];
        foreach (var c in content)
        {
            var lang = c.Lang == "en" ? "en" : "uk";
            var row = await db.Content.FirstOrDefaultAsync(x => x.Key == c.Key && x.Lang == lang, ct);
            if (row is null) db.Content.Add(row = new ContentBlock { Key = c.Key, Lang = lang });
            row.Title = c.Title; row.BodyHtml = c.BodyHtml; row.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);

        var settings = Read<JsonElement?>(dir, "settings.json");
        if (settings is { ValueKind: JsonValueKind.Object } s)
        {
            foreach (var (key, value) in Flatten(s, ""))
            {
                var row = await db.Settings.FirstOrDefaultAsync(x => x.Key == key, ct);
                if (row is null) db.Settings.Add(new Setting { Key = key, Value = value });
                else row.Value = value;
            }
            await db.SaveChangesAsync(ct);
        }

        var redirects = Read<List<ImpRedirect>>(dir, "redirects.json") ?? [];
        foreach (var r in redirects)
        {
            var from = r.From.TrimEnd('/');
            if (from.Length == 0 || from == r.To) continue;
            var row = await db.Redirects.FirstOrDefaultAsync(x => x.FromPath == from, ct);
            if (row is null) db.Redirects.Add(new Redirect { FromPath = from, ToPath = r.To });
            else row.ToPath = r.To;
        }
        await db.SaveChangesAsync(ct);

        // Media: every file under media/ is uploaded under its own name (idempotent).
        var mediaDir = Path.Combine(dir, "media");
        var uploaded = 0;
        if (Directory.Exists(mediaDir))
        {
            foreach (var path in Directory.EnumerateFiles(mediaDir))
            {
                var key = Path.GetFileName(path);
                if (await exists(key, ct)) continue;
                await using var fs = File.OpenRead(path);
                await put(key, fs, ContentType(path), ct);
                uploaded++;
            }
        }

        log.LogInformation("Import done: {Cats} categories, {Prods} products, {Posts} posts, {Vacs} vacancies, {Content} content blocks, {Redirects} redirects, {Media} media files uploaded",
            categories.Count, products.Count, posts.Count, vacancies.Count, content.Count, redirects.Count, uploaded);
    }

    private static T? Read<T>(string dir, string file)
    {
        var path = Path.Combine(dir, file);
        return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json) : default;
    }

    /// {"socials":{"instagram":"…"},"phones":["a","b"]} → socials.instagram=…, phones=a,b
    private static IEnumerable<(string Key, string Value)> Flatten(JsonElement e, string prefix)
    {
        foreach (var prop in e.EnumerateObject())
        {
            var key = prefix + prop.Name;
            switch (prop.Value.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var kv in Flatten(prop.Value, key + ".")) yield return kv;
                    break;
                case JsonValueKind.Array:
                    yield return (key, string.Join(",", prop.Value.EnumerateArray().Select(x => x.ToString())));
                    break;
                case JsonValueKind.Null:
                    break;
                default:
                    yield return (key, prop.Value.ToString());
                    break;
            }
        }
    }

    private static string ContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".svg" => "image/svg+xml",
        ".pdf" => "application/pdf",
        _ => "application/octet-stream",
    };
}
