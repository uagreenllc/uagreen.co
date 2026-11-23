namespace UaGreen.Site.Api.Features.Audit;

/// Which days a visitor may book an audit on: Kyiv-today + 7 … Kyiv-today + 30, inclusive,
/// and only those Gospodar reports free. Gospodar decides "free"; the window is the site's rule.
public static class AuditWindow
{
    public const string ProductSlug = "audit-vitrovih-resursiv";
    private const int FirstDay = 7, LastDay = 30;
    // InvariantGlobalization is on: Linux reads IANA ids straight from tzdata (older tzdata only knows
    // "Europe/Kiev"), Windows can't map IANA → Windows ids without ICU and needs its own "FLE Standard Time".
    private static readonly TimeZoneInfo Kyiv = Find("Europe/Kyiv", "Europe/Kiev", "FLE Standard Time");

    private static TimeZoneInfo Find(params string[] ids)
    {
        foreach (var id in ids)
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz)) return tz;
        throw new TimeZoneNotFoundException($"None of {string.Join(", ", ids)} found");
    }

    public static (DateOnly From, DateOnly To) For(DateTime utcNow)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, Kyiv));
        return (today.AddDays(FirstDay), today.AddDays(LastDay));
    }

    public static bool IsBookable(DateOnly date, DateTime utcNow, IReadOnlyCollection<DateOnly> free)
    {
        var (from, to) = For(utcNow);
        return date >= from && date <= to && free.Contains(date);
    }
}
