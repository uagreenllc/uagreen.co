namespace UaGreen.Site.Api.Features;

/// Public endpoints flatten the `*_uk` / `*_en` pairs by `?lang=`; EN falls back to UK.
public static class Lang
{
    public static string Norm(string? lang) => string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "uk";

    public static string? Pick(string? uk, string? en, string lang)
        => lang == "en" && !string.IsNullOrWhiteSpace(en) ? en : uk;

    public static string PickReq(string uk, string? en, string lang) => Pick(uk, en, lang) ?? uk;
}

/// Shared paging shape for public lists.
public sealed record Page<T>(IReadOnlyList<T> Items, int Total, int PageNumber, int PageSize);
