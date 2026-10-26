using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Persistence;
using UaGreen.Site.Api.Infrastructure.RateLimiting;

namespace UaGreen.Site.Api.Features.Auth;

public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record MeDto(Guid Id, string Email, string FullName, bool IsGospodarAdmin, List<SiteRole> Roles);
public sealed record AuthResponse(MeDto User, string AccessToken, string RefreshToken, DateTime ExpiresAt);

/// Login/refresh are proxied to Gospodar (the IdP); roles come from `site_users`.
public static class AuthEndpoints
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth");

        g.MapPost("/login", async (LoginRequest req, GospodarClient gospodar, AppDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                throw ProblemException.BadRequest("email and password are required");
            var gosp = await gospodar.LoginAsync(req.Email.Trim(), req.Password, ct);
            return Results.Ok(await ToResponseAsync(gosp, ErrorCodes.InvalidCredentials, "Invalid credentials", db, ct));
        }).AllowAnonymous().RequireRateLimiting(RateLimitingExtensions.AuthLogin);

        g.MapPost("/refresh", async (RefreshRequest req, GospodarClient gospodar, AppDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.RefreshToken))
                throw ProblemException.BadRequest("refreshToken is required");
            var gosp = await gospodar.RefreshAsync(req.RefreshToken, ct);
            return Results.Ok(await ToResponseAsync(gosp, ErrorCodes.InvalidToken, "Invalid or expired refresh token", db, ct));
        }).AllowAnonymous().RequireRateLimiting(RateLimitingExtensions.AuthRefresh);

        g.MapGet("/me", async (HttpContext ctx, AppDbContext db, CancellationToken ct) =>
        {
            var id = SiteRoles.UserId(ctx.User) ?? throw ProblemException.Unauthorized("Not authenticated");
            var row = await db.SiteUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
            var roles = await SiteRoles.EffectiveAsync(ctx.User, db, ct);
            return Results.Ok(new MeDto(
                id,
                row?.Email ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "",
                row?.FullName ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "",
                SiteRoles.IsGospodarAdmin(ctx.User),
                roles));
        }).RequireAuthorization();
    }

    private static async Task<AuthResponse> ToResponseAsync(
        GospodarProxyResponse gosp, string unauthorizedCode, string unauthorizedMessage, AppDbContext db, CancellationToken ct)
    {
        if (!gosp.IsSuccess)
        {
            // Never echo Gospodar's raw body to an anonymous caller.
            throw gosp.Status == HttpStatusCode.Unauthorized
                ? ProblemException.Unauthorized(unauthorizedMessage, unauthorizedCode)
                : new ProblemException((int)HttpStatusCode.BadGateway, ErrorCodes.InternalError, "Authentication service error");
        }

        GospodarLoginPayload? payload = null;
        try { payload = JsonSerializer.Deserialize<GospodarLoginPayload>(gosp.Body, Json); } catch (JsonException) { }
        if (payload is null)
            throw new ProblemException((int)HttpStatusCode.BadGateway, ErrorCodes.InternalError, "Unexpected response from auth service");

        var isAdmin = payload.User.Roles.Contains("Admin");
        var row = await db.SiteUsers.FirstOrDefaultAsync(u => u.Id == payload.User.Id, ct);
        // Keep name/email fresh for the admin's user list; roles are assigned here, not in Gospodar.
        if (row is not null && (row.Email != payload.User.Email || row.FullName != payload.User.FullName))
        {
            row.Email = payload.User.Email;
            row.FullName = payload.User.FullName;
            row.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        List<SiteRole> roles = isAdmin ? [SiteRole.Admin] : (row?.Roles ?? []);
        if (!isAdmin && roles.Count == 0)
            throw ProblemException.Forbidden("No back-office access: ask an administrator to assign a role");

        var me = new MeDto(payload.User.Id, payload.User.Email, payload.User.FullName, isAdmin, roles);
        return new AuthResponse(me, payload.AccessToken, payload.RefreshToken, payload.ExpiresAt);
    }
}
