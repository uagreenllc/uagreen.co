namespace UaGreen.Site.Api.Infrastructure.Auth;

/// MUST match Gospodar byte-for-byte: the site validates Gospodar-minted tokens.
public sealed class JwtOptions
{
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string SigningKey { get; set; } = "";
}

public sealed class GospodarOptions
{
    public string BaseUrl { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 30;
    /// How often to poll Gospodar's session-epoch endpoint (deploy cutoff). Min 5s.
    public int SessionEpochPollSeconds { get; set; } = 60;
}

public sealed class AppOptions
{
    /// Public origin of the site (LiqPay result/server URLs, media URLs in emails).
    public string PublicBaseUrl { get; set; } = "";
}
