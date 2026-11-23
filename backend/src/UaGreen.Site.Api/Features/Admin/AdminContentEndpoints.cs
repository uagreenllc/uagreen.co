using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Features.Content;
using UaGreen.Site.Api.Features.Orders;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Persistence;
using static UaGreen.Site.Api.Features.Admin.AdminCommon;

namespace UaGreen.Site.Api.Features.Admin;

public sealed record ContentUpsert(string? Title, string BodyHtml);
public sealed record ReviewUpsert(string? PhotoKey, string TextUk, string? TextEn, string AuthorUk, string? AuthorEn, bool Published, int Sort);

/// Text blocks, testimonials, public settings and the redirect table — Editor.
public static class AdminContentEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin").RequireAuthorization(Policies.Editor);

        g.MapGet("/content", async (AppDbContext db, CancellationToken ct) =>
            await db.Content.AsNoTracking().OrderBy(c => c.Key).ThenBy(c => c.Lang)
                .Select(c => new ContentDto(c.Key, c.Lang, c.Title, c.BodyHtml)).ToListAsync(ct));

        g.MapPut("/content/{key}/{lang}", async (string key, string lang, ContentUpsert req, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
        {
            if (!Slug.IsValid(key.Replace('.', '-')) || key.Length > 80) throw ProblemException.BadRequest("Bad content key");
            var l = Lang.Norm(lang);
            if (l != lang) throw ProblemException.BadRequest("lang must be uk, en or cs");
            var c = await db.Content.FirstOrDefaultAsync(x => x.Key == key && x.Lang == l, ct);
            if (c is null) db.Content.Add(c = new ContentBlock { Key = key, Lang = l });
            c.Title = Clean(req.Title);
            c.BodyHtml = req.BodyHtml ?? "";
            c.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await EvictPublicAsync(cache, ct);
            return Results.Ok(new ContentDto(c.Key, c.Lang, c.Title, c.BodyHtml));
        });

        g.MapDelete("/content/{key}/{lang}", async (string key, string lang, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
        {
            if (await db.Content.Where(x => x.Key == key && x.Lang == lang).ExecuteDeleteAsync(ct) == 0) throw ProblemException.NotFound("Content not found");
            await EvictPublicAsync(cache, ct);
            return Results.NoContent();
        });

        // ponytail: the entity is the admin DTO — flat, no navigations.
        g.MapGet("/reviews", async (AppDbContext db, CancellationToken ct) =>
            await db.Reviews.AsNoTracking().OrderBy(r => r.Sort).ToListAsync(ct));

        g.MapPost("/reviews", async (ReviewUpsert req, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
        {
            var r = new Review { Id = Guid.NewGuid() };
            Apply(r, req);
            db.Reviews.Add(r);
            await db.SaveChangesAsync(ct);
            await EvictPublicAsync(cache, ct);
            return Results.Created($"/api/admin/reviews/{r.Id}", r);
        });

        g.MapPut("/reviews/{id:guid}", async (Guid id, ReviewUpsert req, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
        {
            var r = await db.Reviews.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw ProblemException.NotFound("Review not found");
            Apply(r, req);
            await db.SaveChangesAsync(ct);
            await EvictPublicAsync(cache, ct);
            return Results.Ok(r);
        });

        g.MapDelete("/reviews/{id:guid}", async (Guid id, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
        {
            if (await db.Reviews.Where(x => x.Id == id).ExecuteDeleteAsync(ct) == 0) throw ProblemException.NotFound("Review not found");
            await EvictPublicAsync(cache, ct);
            return Results.NoContent();
        });

        g.MapGet("/settings", async (AppDbContext db, CancellationToken ct) => await SettingsAsync(db, null, ct));

        // Partial update: only the keys sent change; an empty value deletes the key.
        g.MapPut("/settings", async (Dictionary<string, string?> req, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
        {
            if (req.Keys.Any(k => k.StartsWith(CodePrefix) || k.StartsWith(PaymentSettings.Prefix)))
                throw ProblemException.Forbidden("Сторонній код і платіжні системи — лише для адміна");
            await UpsertAsync(req, db, cache, ct);
            return Results.Ok(await SettingsAsync(db, null, ct));
        });

        // Сторонній код: raw HTML/JS/CSS the Nuxt server pastes into every page (server/plugins/site-code.ts).
        // Script injection on the public site, so Admin only, unlike the rest of the settings.
        var a = app.MapGroup("/api/admin/code").RequireAuthorization(Policies.Admin);
        a.MapGet("", async (AppDbContext db, CancellationToken ct) => await SettingsAsync(db, CodePrefix, ct));
        a.MapPut("", async (Dictionary<string, string?> req, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
        {
            if (req.Keys.Any(k => !CodeKeys.Contains(k))) throw ProblemException.BadRequest($"Keys: {string.Join(", ", CodeKeys)}");
            await UpsertAsync(req, db, cache, ct);
            return Results.Ok(await SettingsAsync(db, CodePrefix, ct));
        });

        // Платіжні системи: which checkout methods are on + company requisites for the IBAN transfer.
        var p = app.MapGroup("/api/admin/payments").RequireAuthorization(Policies.Admin);
        p.MapGet("", async (AppDbContext db, CancellationToken ct) => await SettingsAsync(db, PaymentSettings.Prefix, ct));
        p.MapPut("", async (Dictionary<string, string?> req, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
        {
            PaymentSettings.Validate(req);
            await UpsertAsync(req, db, cache, ct);
            return Results.Ok(await SettingsAsync(db, PaymentSettings.Prefix, ct));
        });

        g.MapGet("/redirects", async (AppDbContext db, CancellationToken ct) =>
            await db.Redirects.AsNoTracking().OrderBy(r => r.FromPath).Select(r => new RedirectDto(r.FromPath, r.ToPath)).ToListAsync(ct));

        // Replace the whole table.
        g.MapPut("/redirects", async (List<RedirectDto> req, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
        {
            foreach (var r in req)
                if (!r.From.StartsWith('/') || !r.To.StartsWith('/') || r.From == r.To)
                    throw ProblemException.BadRequest($"Bad redirect: {r.From} -> {r.To}");
            await db.Redirects.ExecuteDeleteAsync(ct);
            db.Redirects.AddRange(req.DistinctBy(r => r.From).Select(r => new Redirect { FromPath = r.From.TrimEnd('/'), ToPath = r.To }));
            await db.SaveChangesAsync(ct);
            await EvictPublicAsync(cache, ct);
            return Results.Ok(req.Count);
        });
    }

    private static void Apply(Review r, ReviewUpsert req)
    {
        if (string.IsNullOrWhiteSpace(req.TextUk)) throw ProblemException.BadRequest("textUk is required");
        if (string.IsNullOrWhiteSpace(req.AuthorUk)) throw ProblemException.BadRequest("authorUk is required");
        r.PhotoKey = Clean(req.PhotoKey);
        r.TextUk = req.TextUk.Trim(); r.TextEn = Clean(req.TextEn);
        r.AuthorUk = req.AuthorUk.Trim(); r.AuthorEn = Clean(req.AuthorEn);
        r.Published = req.Published; r.Sort = req.Sort;
        r.UpdatedAt = DateTime.UtcNow;
    }

    const string CodePrefix = "code_";
    static readonly string[] CodeKeys = ["code_head", "code_body_start", "code_body_end", "code_css"];

    /// prefix null → the plain settings (neither Сторонній код nor Платіжні системи).
    static Task<Dictionary<string, string>> SettingsAsync(AppDbContext db, string? prefix, CancellationToken ct) =>
        (prefix is null
            ? db.Settings.Where(s => !s.Key.StartsWith(CodePrefix) && !s.Key.StartsWith(PaymentSettings.Prefix))
            : db.Settings.Where(s => s.Key.StartsWith(prefix)))
        .AsNoTracking().OrderBy(s => s.Key).ToDictionaryAsync(s => s.Key, s => s.Value, ct);

    static async Task UpsertAsync(Dictionary<string, string?> req, AppDbContext db, IOutputCacheStore cache, CancellationToken ct)
    {
        foreach (var (key, value) in req)
        {
            if (key.Length is 0 or > 80) throw ProblemException.BadRequest("Bad setting key");
            var row = await db.Settings.FirstOrDefaultAsync(s => s.Key == key, ct);
            if (string.IsNullOrWhiteSpace(value)) { if (row is not null) db.Settings.Remove(row); continue; }
            if (row is null) db.Settings.Add(new Setting { Key = key, Value = value.Trim() });
            else row.Value = value.Trim();
        }
        await db.SaveChangesAsync(ct);
        await EvictPublicAsync(cache, ct);
    }
}
