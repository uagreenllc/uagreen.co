using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Geo;
using UaGreen.Site.Api.Infrastructure.Persistence;
using UaGreen.Site.Api.Infrastructure.RateLimiting;

namespace UaGreen.Site.Api.Features.Stats;

/// `url` = location.pathname + search, `ref` = external document.referrer of the landing page.
public sealed record HitDto(string? Url, string? Ref, Guid Visitor, bool Entry);

/// The site's own page-view counter (shown in the admin "Статистика").
public static partial class StatsEndpoints
{
    [GeneratedRegex("bot|crawl|spider|slurp|headless|lighthouse|preview|facebookexternalhit", RegexOptions.IgnoreCase)]
    private static partial Regex Bots();

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/stats/hit", async (HitDto req, HttpContext http, AppDbContext db, GeoIp geo, CancellationToken ct) =>
        {
            if (req.Url is not { Length: > 0 and <= 2000 } || req.Url[0] != '/' || req.Visitor == Guid.Empty)
                throw ProblemException.BadRequest("Bad hit");
            if (Bots().IsMatch(http.Request.Headers.UserAgent.ToString())) return Results.NoContent();

            var q = req.Url.IndexOf('?');
            var path = Uri.UnescapeDataString(q < 0 ? req.Url : req.Url[..q]);
            db.PageViews.Add(new PageView
            {
                At = DateTime.UtcNow,
                Path = path.Length > 300 ? path[..300] : path,
                VisitorId = req.Visitor,
                Entry = req.Entry,
                Source = req.Entry ? Traffic.Classify(req.Ref, q < 0 ? "" : req.Url[q..]) : TrafficSource.Direct,
                Country = geo.Country(http.Connection.RemoteIpAddress),
            });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).AllowAnonymous().RequireRateLimiting(RateLimitingExtensions.Stats);
    }
}

/// Where a session came from: utm tags and ad click ids first, then the referrer host.
public static class Traffic
{
    private static readonly string[] AdClickIds = ["gclid", "gbraid", "wbraid", "dclid", "msclkid"];
    private static readonly HashSet<string> AdMediums = ["cpc", "ppc", "cpm", "paid", "paidsearch", "paid_search", "paidsocial", "paid_social", "display", "banner", "ads"];

    // Checked in this order: "mail.google.com" is Email, not Search.
    private static readonly (TrafficSource Source, string[] Keys)[] Hosts =
    [
        (TrafficSource.Ads, ["googleadservices", "doubleclick", "googlesyndication"]),
        (TrafficSource.Email, ["mail", "outlook", "ukr.net"]),
        (TrafficSource.Social, ["facebook", "fb.com", "fb.me", "instagram", "t.co", "twitter", "x.com", "linkedin", "lnkd.in",
            "youtube", "youtu.be", "tiktok", "t.me", "telegram", "pinterest", "reddit", "viber", "threads", "whatsapp"]),
        (TrafficSource.Search, ["google", "bing", "yahoo", "duckduckgo", "yandex", "baidu", "ecosia", "brave", "startpage", "meta.ua"]),
    ];

    public static TrafficSource Classify(string? referrer, string query)
    {
        var p = QueryHelpers.ParseQuery(query);
        string Q(string k) => p.TryGetValue(k, out var v) ? v.ToString().Trim().ToLowerInvariant() : "";

        var medium = Q("utm_medium");
        if (AdClickIds.Any(p.ContainsKey) || AdMediums.Contains(medium)) return TrafficSource.Ads;
        switch (medium)
        {
            case "email" or "e-mail" or "newsletter": return TrafficSource.Email;
            case "social" or "social-network" or "sm": return TrafficSource.Social;
            case "organic": return TrafficSource.Search;
            case "referral": return TrafficSource.Referral;
        }
        var source = Q("utm_source");
        if (source.Length > 0) return ByHost(source) ?? TrafficSource.Other;

        if (string.IsNullOrWhiteSpace(referrer)) return TrafficSource.Direct;
        // android-app://…, ios-app://… and junk: no host we can trust.
        if (!Uri.TryCreate(referrer, UriKind.Absolute, out var u) || u.Scheme is not ("http" or "https")) return TrafficSource.Other;
        return ByHost(u.Host.ToLowerInvariant()) ?? TrafficSource.Referral;
    }

    /// "google" matches any label (www.google.com.ua); dotted keys match the host or its tail ("t.co", not "microsoft.com").
    private static TrafficSource? ByHost(string host)
    {
        var labels = host.Split('.');
        foreach (var (source, keys) in Hosts)
            if (keys.Any(k => k.Contains('.') ? host == k || host.EndsWith("." + k) : labels.Contains(k)))
                return source;
        return null;
    }
}
