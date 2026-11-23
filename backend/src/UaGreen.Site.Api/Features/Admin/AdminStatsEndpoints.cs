using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Persistence;

namespace UaGreen.Site.Api.Features.Admin;

public sealed record StatsTotals(int Sessions, int PageViews, int Visitors, int Forms);
public sealed record StatsDay(DateOnly Date, int Sessions, int PageViews, int Contacts, int Applications);
public sealed record StatsCount(string Key, int Count);
public sealed record StatsDto(StatsTotals Current, StatsTotals Previous, List<StatsDay> Days, Dictionary<string, int> Sources,
    List<StatsCount> Countries, List<StatsCount> Pages, bool Disabled, string SiteUrl);

/// Site statistics for a date range (Kyiv days, both ends inclusive) vs the same-length range before it.
public static class AdminStatsEndpoints
{
    /// Setting that turns the site counter off (read by frontend/plugins/stats.client.ts).
    public const string DisabledKey = "stats_disabled";

    private static readonly TimeZoneInfo Kyiv =
        TimeZoneInfo.TryFindSystemTimeZoneById("Europe/Kyiv", out var tz) || TimeZoneInfo.TryFindSystemTimeZoneById("Europe/Kiev", out tz)
        || TimeZoneInfo.TryFindSystemTimeZoneById("FLE Standard Time", out tz) ? tz : TimeZoneInfo.Utc;

    private static DateTime Utc(DateOnly d) => TimeZoneInfo.ConvertTimeToUtc(d.ToDateTime(TimeOnly.MinValue), Kyiv);
    private static DateOnly Day(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, Kyiv));

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/stats", async (DateOnly from, DateOnly to, AppDbContext db, IOptions<AppOptions> appOpts, CancellationToken ct) =>
        {
            var len = to.DayNumber - from.DayNumber + 1;
            if (len is < 1 or > 400) throw ProblemException.BadRequest("Bad date range");
            DateTime start = Utc(from.AddDays(-len)), mid = Utc(from), end = Utc(to.AddDays(1));

            // ponytail: both periods pulled into memory and grouped in C# by Kyiv day — fine for tens
            // of thousands of views per range; move the GROUP BY into SQL if traffic grows a lot.
            var views = await db.PageViews.AsNoTracking().Where(v => v.At >= start && v.At < end)
                .Select(v => new { v.At, v.Entry, v.VisitorId, v.Path, v.Source, v.Country }).ToListAsync(ct);
            var contacts = await db.ContactRequests.AsNoTracking().Where(x => x.CreatedAt >= start && x.CreatedAt < end).Select(x => x.CreatedAt).ToListAsync(ct);
            var applications = await db.Applications.AsNoTracking().Where(x => x.CreatedAt >= start && x.CreatedAt < end).Select(x => x.CreatedAt).ToListAsync(ct);

            var cur = views.Where(v => v.At >= mid).ToList();
            var entries = cur.Where(v => v.Entry).ToList();
            StatsTotals Totals(bool current)
            {
                var vs = current ? cur : views.Where(v => v.At < mid).ToList();
                return new(vs.Count(v => v.Entry), vs.Count, vs.Select(v => v.VisitorId).Distinct().Count(),
                    contacts.Concat(applications).Count(t => t >= mid == current));
            }

            var byDay = cur.ToLookup(v => Day(v.At));
            var contactsByDay = contacts.ToLookup(Day);
            var applicationsByDay = applications.ToLookup(Day);
            var days = Enumerable.Range(0, len).Select(i => from.AddDays(i)).Select(d => new StatsDay(d,
                byDay[d].Count(v => v.Entry), byDay[d].Count(), contactsByDay[d].Count(), applicationsByDay[d].Count())).ToList();

            return new StatsDto(
                Totals(true),
                Totals(false),
                days,
                Enum.GetValues<TrafficSource>().ToDictionary(s => s.ToString(), s => entries.Count(v => v.Source == s)),
                [.. entries.Where(v => v.Country != null).GroupBy(v => v.Country!).Select(g => new StatsCount(g.Key, g.Count())).OrderByDescending(c => c.Count)],
                [.. cur.GroupBy(v => v.Path).Select(g => new StatsCount(g.Key, g.Count())).OrderByDescending(c => c.Count).Take(5)],
                await db.Settings.AnyAsync(s => s.Key == DisabledKey, ct),
                appOpts.Value.PublicBaseUrl.TrimEnd('/'));
        }).RequireAuthorization(Policies.SalesOrEditor);
    }
}
