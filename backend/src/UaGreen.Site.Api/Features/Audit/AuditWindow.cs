namespace UaGreen.Site.Api.Features.Audit;

/// Which days a visitor may book an audit on: Kyiv-today + 7 … Kyiv-today + 30, inclusive,
/// and only those Gospodar reports free. Gospodar decides "free"; the window is the site's rule.
public static class AuditWindow
{
    public const string ProductSlug = "audit-vitrovih-resursiv";
    private const int FirstDay = 7, LastDay = 30;
    private static readonly TimeZoneInfo Kyiv = TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv");

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
