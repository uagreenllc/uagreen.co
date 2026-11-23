using System.Security.Claims;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Persistence;

namespace UaGreen.Site.Api.Features.Admin;

public static class AdminCommon
{
    public const string PublicCacheTag = "public";

    /// Admin writes evict the 60 s public cache so a price change is live at once.
    public static ValueTask EvictPublicAsync(IOutputCacheStore cache, CancellationToken ct)
        => cache.EvictByTagAsync(PublicCacheTag, ct);

    public static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// `%q%` for ILIKE, with the user's own wildcards escaped.
    public static string Like(string q) => "%" + q.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";

    /// One page of an already-ordered query; the admin lists load these as you scroll.
    public static async Task<Page<TOut>> PageAsync<T, TOut>(IQueryable<T> q, int? page, int? pageSize, Func<T, TOut> map, CancellationToken ct)
    {
        var pn = Math.Max(1, page ?? 1);
        var ps = Math.Clamp(pageSize ?? 30, 1, 200);
        var total = await q.CountAsync(ct);
        var rows = await q.Skip((pn - 1) * ps).Take(ps).ToListAsync(ct);
        return new Page<TOut>(rows.Select(map).ToList(), total, pn, ps);
    }

    /// Display name for history entries ("Іван Петренко"), falling back to the id.
    public static string Actor(ClaimsPrincipal user)
        => user.FindFirstValue("full_name") ?? user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue("email")
            ?? user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "?";

    /// A slug change keeps old links alive: 301 old → new for both locales, earlier
    /// redirects to the old path are re-pointed (no chains), and a redirect sitting on
    /// the new path is dropped so it doesn't bounce visitors off the live page.
    public static async Task RedirectSlugAsync(AppDbContext db, string prefix, string oldSlug, string newSlug, CancellationToken ct)
    {
        if (oldSlug == newSlug || string.IsNullOrEmpty(oldSlug)) return;
        foreach (var lang in new[] { "", "/en" })
        {
            var from = $"{lang}{prefix}/{oldSlug}";
            var to = $"{lang}{prefix}/{newSlug}";
            await db.Redirects.Where(r => r.FromPath == to).ExecuteDeleteAsync(ct);
            await db.Redirects.Where(r => r.ToPath == from).ExecuteUpdateAsync(s => s.SetProperty(r => r.ToPath, to), ct);
            var row = await db.Redirects.FirstOrDefaultAsync(r => r.FromPath == from, ct);
            if (row is null) db.Redirects.Add(new Redirect { FromPath = from, ToPath = to });
            else row.ToPath = to;
        }
    }
}
