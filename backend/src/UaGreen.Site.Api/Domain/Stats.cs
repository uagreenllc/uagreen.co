namespace UaGreen.Site.Api.Domain;

public enum TrafficSource { Direct, Search, Referral, Social, Ads, Email, Other }

/// One page view, sent by the site's own counter (frontend/plugins/stats.client.ts).
/// No IP or user agent is kept — only the country looked up from the IP.
public sealed class PageView
{
    public long Id { get; set; }
    public DateTime At { get; set; }
    public string Path { get; set; } = "";
    /// Random id the browser keeps in localStorage (unique visitors).
    public Guid VisitorId { get; set; }
    /// First view of a session (after 30 min without views). Sessions, traffic
    /// sources and countries count these rows; Source is Direct on the rest.
    public bool Entry { get; set; }
    public TrafficSource Source { get; set; }
    public string? Country { get; set; }
}
