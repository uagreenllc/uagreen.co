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

/// Text blocks, public settings and the redirect table — Editor.
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
            if (l != lang) throw ProblemException.BadRequest("lang must be uk or en");
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

        g.MapGet("/settings", async (AppDbContext db, CancellationToken ct) =>
            await db.Settings.AsNoTracking().OrderBy(s => s.Key).ToDictionaryAsync(s => s.Key, s => s.Value, ct));

        // Partial update: only the keys sent change; an empty value deletes the key.
        g.MapPut("/settings", async (Dictionary<string, string?> req, AppDbContext db, IOutputCacheStore cache, CancellationToken ct) =>
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
            return Results.Ok(await db.Settings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value, ct));
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
}
