using Microsoft.AspNetCore.OutputCaching;

namespace UaGreen.Site.Api.Features.Admin;

public static class AdminCommon
{
    public const string PublicCacheTag = "public";

    /// Admin writes evict the 60 s public cache so a price change is live at once.
    public static ValueTask EvictPublicAsync(IOutputCacheStore cache, CancellationToken ct)
        => cache.EvictByTagAsync(PublicCacheTag, ct);

    public static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
