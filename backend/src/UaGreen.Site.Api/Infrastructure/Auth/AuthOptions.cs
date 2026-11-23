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
    /// Company integration key (Компанії → ключ інтеграції in Gospodar) for pushing
    /// contact-form requests to the CRM. Empty = CRM push is off.
    public string IntegrationKey { get; set; } = "";
    /// Cap for integration calls a visitor waits on (free audit days). Without it the HttpClient's
    /// resilience retries keep a page waiting ~25 s when Gospodar is down.
    public TimeSpan IntegrationTimeout { get; set; } = TimeSpan.FromSeconds(5);
    /// Browser origin of the ERP for deal links in the admin; defaults to BaseUrl.
    public string? WebUrl { get; set; }
}

public sealed class AppOptions
{
    /// Public origin of the site (LiqPay result/server URLs, media URLs in emails).
    public string PublicBaseUrl { get; set; } = "";
}
