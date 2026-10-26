using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Features.Admin;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Email;
using UaGreen.Site.Api.Infrastructure.Import;
using UaGreen.Site.Api.Infrastructure.Payments;
using UaGreen.Site.Api.Infrastructure.Persistence;
using UaGreen.Site.Api.Infrastructure.RateLimiting;
using UaGreen.Site.Api.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")).UseSnakeCaseNamingConvention());

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection("App"));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection("Notifications"));
builder.Services.Configure<LiqPayOptions>(builder.Configuration.GetSection("LiqPay"));
builder.Services.Configure<FileStorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.AddOptions<GospodarOptions>()
    .Bind(builder.Configuration.GetSection("Gospodar"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "Gospodar:BaseUrl is required.")
    .ValidateOnStart();

builder.Services.AddHttpClient<GospodarClient>((sp, http) =>
{
    var opts = sp.GetRequiredService<IOptions<GospodarOptions>>().Value;
    http.BaseAddress = new Uri(opts.BaseUrl);
    http.Timeout = TimeSpan.FromSeconds(Math.Max(60, opts.TimeoutSeconds * 2));
}).AddStandardResilienceHandler();
builder.Services.AddSingleton<SessionEpochCache>();
builder.Services.AddHostedService<SessionEpochRefresher>();

// JWT: Gospodar-minted tokens, same key/issuer/audience (copied from TTApp).
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwtOpts) =>
    {
        var jwt = jwtOpts.Value;
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
        bearer.Events = new JwtBearerEvents
        {
            OnTokenValidated = ctx =>
            {
                var iat = ctx.Principal?.FindFirst(JwtRegisteredClaimNames.Iat)?.Value;
                if (iat is not null && long.TryParse(iat, out var iatUnix))
                {
                    var cache = ctx.HttpContext.RequestServices.GetRequiredService<SessionEpochCache>();
                    if (SessionEpochPolicy.IsInvalidated(DateTimeOffset.FromUnixTimeSeconds(iatUnix).UtcDateTime, cache.ValidFromUtc))
                        ctx.Fail("Session invalidated by Gospodar deploy cutoff.");
                }
                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorizationBuilder().AddSitePolicies();
builder.Services.AddScoped<IAuthorizationHandler, SiteRoleHandler>();

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    p.WithOrigins(origins).WithMethods("GET", "POST", "PUT", "PATCH", "DELETE").WithHeaders("Authorization", "Content-Type");
}));

builder.Services.AddAppRateLimiting(builder.Configuration);
builder.Services.AddOutputCache(o =>
    o.AddPolicy("public", p => p.Expire(TimeSpan.FromSeconds(60)).SetVaryByQuery("*").Tag(AdminCommon.PublicCacheTag)));

builder.Services.AddSingleton<FileStorage>();
builder.Services.AddScoped<Notifier>();
builder.Services.AddSingleton<LiqPayService>();
builder.Services.AddHostedService<ImportRunner>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    // Single instance, no migration job, runtime image has no `dotnet ef`: migrate on boot.
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

// Behind the stack nginx (and the shared TLS nginx): restore the client IP / scheme
// so per-IP rate limits and LiqPay URLs see the real values.
var fwd = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto };
fwd.KnownNetworks.Clear();
fwd.KnownProxies.Clear();
fwd.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("172.16.0.0"), 12));
fwd.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("10.0.0.0"), 8));
fwd.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("192.168.0.0"), 16));
app.UseForwardedHeaders(fwd);

app.UseMiddleware<ProblemMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.UseOutputCache();

app.MapGet("/", () => "UA GREEN site API");
app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "ok" }) : Results.StatusCode(503));

UaGreen.Site.Api.Features.Auth.AuthEndpoints.Map(app);
UaGreen.Site.Api.Features.Catalog.CatalogEndpoints.Map(app);
UaGreen.Site.Api.Features.Blog.BlogEndpoints.Map(app);
UaGreen.Site.Api.Features.Vacancies.VacanciesEndpoints.Map(app);
UaGreen.Site.Api.Features.Content.ContentEndpoints.Map(app);
UaGreen.Site.Api.Features.Orders.OrdersEndpoints.Map(app);
UaGreen.Site.Api.Features.Forms.FormsEndpoints.Map(app);

AdminCatalogEndpoints.Map(app);
AdminBlogEndpoints.Map(app);
AdminVacanciesEndpoints.Map(app);
AdminOrdersEndpoints.Map(app);
AdminContentEndpoints.Map(app);
AdminInboxEndpoints.Map(app);
AdminUsersEndpoints.Map(app);
AdminMediaEndpoints.Map(app);

app.Run();

public partial class Program;
