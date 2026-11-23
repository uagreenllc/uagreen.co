namespace UaGreen.Site.Api.Features;

/// Public endpoints flatten the `*_uk` / `*_en` pairs by `?lang=`; EN falls back to UK.
/// ponytail: only category names have a CS column; everything else shows EN (then UK) for CS. Add `*Cs` columns when Czech copy exists.
public static class Lang
{
    public static string Norm(string? lang) => lang?.ToLowerInvariant() switch { "en" => "en", "cs" => "cs", _ => "uk" };

    /// Content-block langs to try, best first.
    public static string[] Chain(string lang) => lang switch { "cs" => ["cs", "en", "uk"], "en" => ["en", "uk"], _ => ["uk"] };

    public static string? Pick(string? uk, string? en, string lang, string? cs = null)
        => lang == "cs" && !string.IsNullOrWhiteSpace(cs) ? cs
         : lang != "uk" && !string.IsNullOrWhiteSpace(en) ? en : uk;

    public static string PickReq(string uk, string? en, string lang, string? cs = null) => Pick(uk, en, lang, cs) ?? uk;
}

/// Shared paging shape for public lists.
public sealed record Page<T>(IReadOnlyList<T> Items, int Total, int PageNumber, int PageSize);
