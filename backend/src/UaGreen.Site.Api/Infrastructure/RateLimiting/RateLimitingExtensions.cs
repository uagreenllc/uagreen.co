using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using UaGreen.Site.Api.Common;

namespace UaGreen.Site.Api.Infrastructure.RateLimiting;

public static class RateLimitingExtensions
{
    public const string AuthLogin = "auth-login";
    public const string AuthRefresh = "auth-refresh";
    /// Public POSTs: orders, applications, contact.
    public const string Forms = "forms";
    public const string Uploads = "uploads";

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection s, IConfiguration cfg)
    {
        s.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = 429;
            o.OnRejected = async (ctx, _) =>
            {
                ctx.HttpContext.Response.Headers["Retry-After"] = "60";
                ctx.HttpContext.Response.ContentType = "application/json";
                await ctx.HttpContext.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(
                    new ApiError(ErrorCodes.RateLimited, "Too many requests"),
                    new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }));
            };

            o.AddPolicy(AuthLogin, ctx => RateLimitPartition.GetFixedWindowLimiter(Ip(ctx), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = cfg.GetValue("RateLimiting:Login:PermitLimit", 5),
                Window = cfg.GetValue("RateLimiting:Login:Window", TimeSpan.FromMinutes(15)),
                QueueLimit = 0,
            }));

            o.AddPolicy(AuthRefresh, ctx => RateLimitPartition.GetTokenBucketLimiter(Ip(ctx), _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = cfg.GetValue("RateLimiting:Refresh:PermitLimit", 30),
                TokensPerPeriod = cfg.GetValue("RateLimiting:Refresh:PermitLimit", 30),
                ReplenishmentPeriod = cfg.GetValue("RateLimiting:Refresh:Window", TimeSpan.FromMinutes(1)),
                AutoReplenishment = true,
                QueueLimit = 0,
            }));

            o.AddPolicy(Forms, ctx => RateLimitPartition.GetFixedWindowLimiter(Ip(ctx), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = cfg.GetValue("RateLimiting:Forms:PermitLimit", 10),
                Window = cfg.GetValue("RateLimiting:Forms:Window", TimeSpan.FromHours(1)),
                QueueLimit = 0,
            }));

            o.AddPolicy(Uploads, ctx => RateLimitPartition.GetTokenBucketLimiter(
                ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? Ip(ctx), _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = cfg.GetValue("RateLimiting:Uploads:PermitLimit", 60),
                TokensPerPeriod = cfg.GetValue("RateLimiting:Uploads:PermitLimit", 60),
                ReplenishmentPeriod = cfg.GetValue("RateLimiting:Uploads:Window", TimeSpan.FromMinutes(1)),
                AutoReplenishment = true,
                QueueLimit = 0,
            }));
        });
        return s;
    }

    private static string Ip(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
