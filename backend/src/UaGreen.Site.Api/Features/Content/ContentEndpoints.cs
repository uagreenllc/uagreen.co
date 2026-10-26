using Microsoft.EntityFrameworkCore;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Infrastructure.Persistence;

namespace UaGreen.Site.Api.Features.Content;

public sealed record ContentDto(string Key, string Lang, string? Title, string BodyHtml);
public sealed record RedirectDto(string From, string To);

/// Editable text blocks, public settings and the old-URL redirect table.
public static class ContentEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api").AllowAnonymous().CacheOutput("public");

        // ?keys=home.hero,home.services — one call for a whole page.
        g.MapGet("/content", async (string? keys, string? lang, AppDbContext db, CancellationToken ct) =>
        {
            var l = Lang.Norm(lang);
            var wanted = (keys ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var q = db.Content.AsNoTracking().Where(c => c.Lang == l || c.Lang == "uk");
            if (wanted.Length > 0) q = q.Where(c => wanted.Contains(c.Key));
            var rows = await q.ToListAsync(ct);
            // EN falls back to UK per key.
            return rows.GroupBy(c => c.Key)
                .Select(grp => grp.FirstOrDefault(c => c.Lang == l) ?? grp.First())
                .Select(c => new ContentDto(c.Key, c.Lang, c.Title, c.BodyHtml));
        });

        g.MapGet("/content/{key}", async (string key, string? lang, AppDbContext db, CancellationToken ct) =>
        {
            var l = Lang.Norm(lang);
            var c = await db.Content.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key && x.Lang == l, ct)
                ?? await db.Content.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key && x.Lang == "uk", ct)
                ?? throw ProblemException.NotFound("Content not found");
            return new ContentDto(c.Key, c.Lang, c.Title, c.BodyHtml);
        });

        g.MapGet("/settings/public", async (AppDbContext db, CancellationToken ct) =>
            await db.Settings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value, ct));

        g.MapGet("/redirects", async (AppDbContext db, CancellationToken ct) =>
            await db.Redirects.AsNoTracking().Select(r => new RedirectDto(r.FromPath, r.ToPath)).ToListAsync(ct));
    }
}
