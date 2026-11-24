using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace UaGreen.Site.Api.Infrastructure.Auth;

public sealed record GospodarProxyResponse(HttpStatusCode Status, string Body, bool IsSuccess);

/// Shape of Gospodar's POST /api/auth/login and /refresh responses.
public sealed record GospodarLoginPayload(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    GospodarUserPayload User);

public sealed record GospodarUserPayload(
    Guid Id,
    string Email,
    string FullName,
    bool IsActive,
    string[] Roles);

/// Body of Gospodar's POST /api/integration/audit-bookings — a paid site audit (idempotent by ExternalId).
public sealed record GospodarAuditBooking(string ExternalId, string Name, string Phone, string? Email, string Address, string? Comment,
    DateOnly Date, decimal Amount, DateTime PaidAt, string PaymentRef, DateTime OfferAcceptedAt);

/// Gospodar's answer: DateWasFree = false when the day was taken meanwhile (booked anyway, manager reschedules).
public sealed record GospodarAuditBooked(Guid DealId, Guid OrderId, Guid TripId, bool DateWasFree);

/// Gospodar's GET /api/integration/requisites: the company the site key belongs to.
public sealed record GospodarRequisites(string? Recipient, string? Edrpou, string? Iban, string? Bank);

/// One user as returned by Gospodar's GET /api/users.
public sealed record GospodarUserDto(Guid Id, string? Email, string? FullName, bool IsActive, IReadOnlyList<string>? Roles);

/// Gospodar (erp.uagreen.co) is the identity provider: login/refresh are proxied,
/// user listing uses a short-lived service token signed with the shared key —
/// a trimmed copy of TTApp's GospodarClient. Site leads go to the CRM through CrmSync.
public sealed class GospodarClient(HttpClient http, IOptions<JwtOptions> jwt, IOptions<GospodarOptions> gospodar, ILogger<GospodarClient> log)
{
    private readonly JwtOptions _jwt = jwt.Value;
    private readonly string _apiKey = gospodar.Value.IntegrationKey;
    private readonly TimeSpan _visitorTimeout = gospodar.Value.IntegrationTimeout;

    public Task<GospodarProxyResponse> LoginAsync(string email, string password, CancellationToken ct)
        => PostAsync("/api/auth/login", new { email, password }, ct);

    public Task<GospodarProxyResponse> RefreshAsync(string refreshToken, CancellationToken ct)
        => PostAsync("/api/auth/refresh", new { refreshToken }, ct);

    /// Gospodar's deploy cutoff; null on any failure so the caller keeps its last-known value.
    public async Task<DateTime?> GetSessionEpochAsync(CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync("/api/auth/session-epoch", ct);
            if (!response.IsSuccessStatusCode) return null;
            var dto = await response.Content.ReadFromJsonAsync<SessionEpochDto>(ct);
            return dto?.ValidFromUtc;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// Every Gospodar user (for the role-assignment screen). Null on failure.
    public async Task<IReadOnlyList<GospodarUserDto>?> GetUsersAsync(CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/api/users");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", MintPlatformToken());
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<List<GospodarUserDto>>(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// Days in [from, to] with a free audit specialist. Null when Gospodar can't tell (no key, down, error) —
    /// the booking page then shows its "can't show dates" fallback instead of an empty calendar.
    public async Task<IReadOnlyList<DateOnly>?> GetAuditSlotsAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        using var req = Integration(HttpMethod.Get, $"/api/integration/audit-slots?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
        if (req is null) return null;
        using var resp = await SendAsync(req, "audit-slots", ct, _visitorTimeout);
        if (resp is null) return null;
        try
        {
            return (await resp.Content.ReadFromJsonAsync<AuditSlotsPayload>(ct))?.Dates;
        }
        catch (System.Text.Json.JsonException ex)
        {
            log.LogWarning(ex, "Gospodar audit-slots: malformed response");
            return null;
        }
    }

    private sealed record AuditSlotsPayload(List<DateOnly> Dates);

    /// Requisites of the site key's company (Компанії → реквізити) for the IBAN transfer. Null when Gospodar can't tell.
    public async Task<GospodarRequisites?> GetRequisitesAsync(CancellationToken ct)
    {
        using var req = Integration(HttpMethod.Get, "/api/integration/requisites");
        if (req is null) return null;
        using var resp = await SendAsync(req, "requisites", ct, _visitorTimeout);
        if (resp is null) return null;
        try
        {
            return await resp.Content.ReadFromJsonAsync<GospodarRequisites>(ct);
        }
        catch (System.Text.Json.JsonException ex)
        {
            log.LogWarning(ex, "Gospodar requisites: malformed response");
            return null;
        }
    }

    /// Books a paid audit; null when it didn't happen (no key, Gospodar down or refused) — the caller emails managers.
    public async Task<GospodarAuditBooked?> BookAuditAsync(GospodarAuditBooking booking, CancellationToken ct)
    {
        using var req = Integration(HttpMethod.Post, "/api/integration/audit-bookings");
        if (req is null) return null;
        req.Content = JsonContent.Create(booking);
        using var resp = await SendAsync(req, "audit-booking", ct);
        if (resp is null) return null;
        try
        {
            return await resp.Content.ReadFromJsonAsync<GospodarAuditBooked>(ct);
        }
        catch (System.Text.Json.JsonException ex)
        {
            log.LogWarning(ex, "Gospodar audit-booking: malformed response");
            return null;
        }
    }

    /// Null when the site has no integration key: integration calls are then skipped, not failed.
    private HttpRequestMessage? Integration(HttpMethod method, string path)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            log.LogInformation("Gospodar integration key not configured, skipping {Path}", path);
            return null;
        }
        var req = new HttpRequestMessage(method, path);
        req.Headers.Add("X-Api-Key", _apiKey);
        return req;
    }

    /// The response on 2xx, null (logged) on anything else — the visitor's request never fails because of Gospodar.
    /// `timeout` caps the whole call including the resilience retries; null = the HttpClient's own limits
    /// (the paid-audit booking runs in the LiqPay callback, where waiting is fine and giving up early is not).
    private async Task<HttpResponseMessage?> SendAsync(HttpRequestMessage req, string what, CancellationToken ct, TimeSpan? timeout = null)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (timeout is { } t) cts.CancelAfter(t);
        try
        {
            var resp = await http.SendAsync(req, cts.Token);
            if (resp.IsSuccessStatusCode) return resp;
            log.LogWarning("Gospodar {What}: {Status} {Body}", what, (int)resp.StatusCode, await resp.Content.ReadAsStringAsync(ct));
            resp.Dispose();
        }
        // Polly's own rejections (total timeout, open circuit) come from AddStandardResilienceHandler, not HttpClient.
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or Polly.ExecutionRejectedException)
        {
            log.LogWarning(ex, "Gospodar {What} failed", what);
        }
        return null;
    }

    // Platform service principal (Admin + platform scope), 5-minute shared-key token —
    // the mirror of Gospodar's own service-token minting.
    private string MintPlatformToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey));
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.Empty.ToString()),
                new Claim("scope", "platform"),
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record SessionEpochDto(DateTime ValidFromUtc);

    private async Task<GospodarProxyResponse> PostAsync<TBody>(string path, TBody body, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(path, body, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        return new GospodarProxyResponse(response.StatusCode, text, response.IsSuccessStatusCode);
    }
}
