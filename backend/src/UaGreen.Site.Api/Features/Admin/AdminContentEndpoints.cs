using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Features.Content;
using UaGreen.Site.Api.Features.Orders;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Payments;
using UaGreen.Site.Api.Infrastructure.Persistence;
using static UaGreen.Site.Api.Features.Admin.AdminCommon;

namespace UaGreen.Site.Api.Features.Admin;

public sealed record ContentUpsert(string? Title, string BodyHtml);
public sealed record RedirectUpsert(string From, string To, string? Original);
public sealed record ReviewUpsert(string? PhotoKey, string TextUk, string? TextEn, string AuthorUk, string? AuthorEn, bool Published, int Sort,
    string? TextCs = null, string? AuthorCs = null);

/// Text blocks, testimonials, public settings and the redirect table — Editor.
public static class AdminContentEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin").RequireAuthorization(Policies.Editor);

        g.MapGet("/content", async (string? q, string? lang, int? page, int? pageSize, AppDbContext db, CancellationToken ct) =>
        {
            var query = db.Content.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(lang)) query = query.Where(c => c.Lang == lang);
            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(c => EF.Functions.ILike(c.Key, Like(q)) || (c.Title != null && EF.Functions.ILike(c.Title, Like(q))) || EF.Functions.ILike(c.BodyHtml, Like(q)));
            return await PageAsync(query.OrderBy(c => c.Key).ThenBy(c => c.Lang), page, pageSize,
                c => new ContentDto(c.Key, c.Lang, c.Title, c.BodyHtml), ct);
        });

        g.MapPut("/content/{key}/{lang}", async (string key, string lang, ContentUpsert req, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
        {
            if (!Slug.IsValid(key.Replace('.', '-')) || key.Length > 80) throw ProblemException.BadRequest("Ключ: латиниця, цифри, крапки й дефіси (напр. page.about)");
            var l = Lang.Norm(lang);
            if (l != lang) throw ProblemException.BadRequest("Мова — uk, en або cs");
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
            if (await db.Content.Where(x => x.Key == key && x.Lang == lang).ExecuteDeleteAsync(ct) == 0) throw ProblemException.NotFound("Блок не знайдено");
            await EvictPublicAsync(cache, ct);
            return Results.NoContent();
        });

        // ponytail: the entity is the admin DTO — flat, no navigations.
        g.MapGet("/reviews", async (string? q, int? page, int? pageSize, AppDbContext db, CancellationToken ct) =>
        {
            var query = db.Reviews.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(q)) query = query.Where(r => EF.Functions.ILike(r.AuthorUk, Like(q)) || EF.Functions.ILike(r.TextUk, Like(q)));
            return await PageAsync(query.OrderBy(r => r.Sort), page, pageSize, r => r, ct);
        });

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
            var r = await db.Reviews.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw ProblemException.NotFound("Відгук не знайдено");
            Apply(r, req);
            await db.SaveChangesAsync(ct);
            await EvictPublicAsync(cache, ct);
            return Results.Ok(r);
        });

        g.MapDelete("/reviews/{id:guid}", async (Guid id, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
        {
            if (await db.Reviews.Where(x => x.Id == id).ExecuteDeleteAsync(ct) == 0) throw ProblemException.NotFound("Відгук не знайдено");
            await EvictPublicAsync(cache, ct);
            return Results.NoContent();
        });

        g.MapGet("/settings", async (AppDbContext db, CancellationToken ct) => await SettingsAsync(db, null, ct));

        // Partial update: only the keys sent change; an empty value deletes the key.
        g.MapPut("/settings", async (Dictionary<string, string?> req, HttpContext http, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
        {
            if (req.Keys.Any(k => k.StartsWith(CodePrefix) || k.StartsWith(PaymentSettings.Prefix)))
                throw ProblemException.Forbidden("Сторонній код і платіжні системи — лише для адміна");
            if (req.Keys.Any(k => k.StartsWith("admin.")) && !(await SiteRoles.EffectiveAsync(http.User, db, ct)).Contains(SiteRole.Admin))
                throw ProblemException.Forbidden("Ключі admin.* змінює лише адміністратор");
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
        // Opening the page pulls the requisites from Gospodar right away (PaymentRequisitesSync does it every 15 min).
        var p = app.MapGroup("/api/admin/payments").RequireAuthorization(Policies.Admin);
        p.MapGet("", async (AppDbContext db, GospodarClient gospodar, IOutputCacheStore cache, ILogger<PaymentRequisitesSync> log, CancellationToken ct) =>
        {
            await PaymentSettings.SyncAsync(db, gospodar, cache, log, ct);
            return await SettingsAsync(db, PaymentSettings.Prefix, ct);
        });
        // Which methods have their bank keys on the server (the page warns about the ones that don't).
        p.MapGet("/configured", (IOptions<MonobankOptions> card, IOptions<MonoPartsOptions> parts) =>
        {
            var missing = PaymentSettings.Unconfigured(card.Value, parts.Value);
            return new Dictionary<string, bool>
            {
                [nameof(PaymentMethod.Card)] = !missing.Contains(PaymentMethod.Card),
                [nameof(PaymentMethod.MonoParts)] = !missing.Contains(PaymentMethod.MonoParts),
            };
        });
        p.MapPut("", async (Dictionary<string, string?> req, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
        {
            PaymentSettings.Validate(req, await SettingsAsync(db, PaymentSettings.Prefix, ct));
            await UpsertAsync(req, db, cache, ct);
            return Results.Ok(await SettingsAsync(db, PaymentSettings.Prefix, ct));
        });

        g.MapGet("/redirects", async (string? q, int? page, int? pageSize, AppDbContext db, CancellationToken ct) =>
        {
            var query = db.Redirects.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(q)) query = query.Where(r => EF.Functions.ILike(r.FromPath, Like(q)) || EF.Functions.ILike(r.ToPath, Like(q)));
            return await PageAsync(query.OrderBy(r => r.FromPath), page, pageSize, r => new RedirectDto(r.FromPath, r.ToPath), ct);
        });

        // One row: add, or edit (`original` = the row's old From when From itself changed).
        g.MapPost("/redirects/item", async (RedirectUpsert req, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
        {
            var from = Path(req.From);
            var to = (req.To ?? "").Trim();
            if (!from.StartsWith('/') || !to.StartsWith('/')) throw ProblemException.BadRequest("Обидві адреси мають починатися з /");
            if (from == Path(to)) throw ProblemException.BadRequest("Адреса не може вести сама на себе");
            if (await db.Redirects.AnyAsync(r => r.FromPath == Path(to), ct)) throw ProblemException.BadRequest($"{to} сам є редиректом — вкажіть кінцеву адресу");
            if (req.Original is { } orig && Path(orig) != from) await db.Redirects.Where(r => r.FromPath == Path(orig)).ExecuteDeleteAsync(ct);
            var row = await db.Redirects.FirstOrDefaultAsync(r => r.FromPath == from, ct);
            if (row is null) db.Redirects.Add(new Redirect { FromPath = from, ToPath = to });
            else row.ToPath = to;
            await db.SaveChangesAsync(ct);
            await EvictPublicAsync(cache, ct);
            return Results.Ok(new RedirectDto(from, to));
        });

        g.MapDelete("/redirects/item", async (string from, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
        {
            if (await db.Redirects.Where(r => r.FromPath == from).ExecuteDeleteAsync(ct) == 0) throw ProblemException.NotFound("Редирект не знайдено");
            await EvictPublicAsync(cache, ct);
            return Results.NoContent();
        });

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

    private static string Path(string? p)
    {
        var t = (p ?? "").Trim();
        return t.Length > 1 ? t.TrimEnd('/') : t;
    }

    private static void Apply(Review r, ReviewUpsert req)
    {
        if (string.IsNullOrWhiteSpace(req.TextUk)) throw ProblemException.BadRequest("Вкажіть текст відгуку (UK)");
        if (string.IsNullOrWhiteSpace(req.AuthorUk)) throw ProblemException.BadRequest("Вкажіть автора (UK)");
        r.PhotoKey = Clean(req.PhotoKey);
        r.TextUk = req.TextUk.Trim(); r.TextEn = Clean(req.TextEn);
        r.AuthorUk = req.AuthorUk.Trim(); r.AuthorEn = Clean(req.AuthorEn);
        r.TextCs = Clean(req.TextCs); r.AuthorCs = Clean(req.AuthorCs);
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
            if (key.Length is 0 or > 80) throw ProblemException.BadRequest($"Ключ «{key}»: від 1 до 80 символів");
            var row = await db.Settings.FirstOrDefaultAsync(s => s.Key == key, ct);
            if (string.IsNullOrWhiteSpace(value)) { if (row is not null) db.Settings.Remove(row); continue; }
            if (row is null) db.Settings.Add(new Setting { Key = key, Value = value.Trim() });
            else row.Value = value.Trim();
        }
        await db.SaveChangesAsync(ct);
        await EvictPublicAsync(cache, ct);
    }
}
