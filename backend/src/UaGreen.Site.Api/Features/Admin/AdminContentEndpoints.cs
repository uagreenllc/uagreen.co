using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Features.Content;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Persistence;
using static UaGreen.Site.Api.Features.Admin.AdminCommon;

namespace UaGreen.Site.Api.Features.Admin;

public sealed record ContentUpsert(string? Title, string BodyHtml);
public sealed record RedirectUpsert(string From, string To, string? Original);

/// Text blocks, public settings and the redirect table — Editor.
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
            if (l != lang) throw ProblemException.BadRequest("Мова — uk або en");
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

        g.MapGet("/settings", async (AppDbContext db, CancellationToken ct) =>
            await db.Settings.AsNoTracking().OrderBy(s => s.Key).ToDictionaryAsync(s => s.Key, s => s.Value, ct));

        // Partial update: only the keys sent change; an empty value deletes the key.
        g.MapPut("/settings", async (Dictionary<string, string?> req, HttpContext http, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
        {
            if (req.Keys.Any(k => k.StartsWith("admin.")) && !(await SiteRoles.EffectiveAsync(http.User, db, ct)).Contains(SiteRole.Admin))
                throw ProblemException.Forbidden("Ключі admin.* змінює лише адміністратор");
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
            return Results.Ok(await db.Settings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value, ct));
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
}
