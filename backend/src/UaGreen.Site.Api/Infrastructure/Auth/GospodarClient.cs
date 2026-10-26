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

/// One user as returned by Gospodar's GET /api/users.
public sealed record GospodarUserDto(Guid Id, string? Email, string? FullName, bool IsActive, IReadOnlyList<string>? Roles);

/// Gospodar (erp.uagreen.co) is the identity provider: login/refresh are proxied,
/// user listing uses a short-lived service token signed with the shared key —
/// a trimmed copy of TTApp's GospodarClient.
public sealed class GospodarClient(HttpClient http, IOptions<JwtOptions> jwt)
{
    private readonly JwtOptions _jwt = jwt.Value;

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
