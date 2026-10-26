using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Persistence;

namespace UaGreen.Site.Api.Infrastructure.Auth;

public static class Policies
{
    public const string Admin = "site-admin";
    public const string Sales = "site-sales";
    public const string Editor = "site-editor";
    public const string Hr = "site-hr";
    /// Contact requests and media uploads: Sales or Editor.
    public const string SalesOrEditor = "site-sales-or-editor";

    public static AuthorizationBuilder AddSitePolicies(this AuthorizationBuilder b) => b
        .AddPolicy(Admin, p => p.RequireAuthenticatedUser().AddRequirements(new SiteRoleRequirement(SiteRole.Admin)))
        .AddPolicy(Sales, p => p.RequireAuthenticatedUser().AddRequirements(new SiteRoleRequirement(SiteRole.Sales)))
        .AddPolicy(Editor, p => p.RequireAuthenticatedUser().AddRequirements(new SiteRoleRequirement(SiteRole.Editor)))
        .AddPolicy(Hr, p => p.RequireAuthenticatedUser().AddRequirements(new SiteRoleRequirement(SiteRole.Hr)))
        .AddPolicy(SalesOrEditor, p => p.RequireAuthenticatedUser().AddRequirements(new SiteRoleRequirement(SiteRole.Sales, SiteRole.Editor)));
}

/// Satisfied by a Gospodar Admin, a site Admin, or any of the listed site roles.
public sealed class SiteRoleRequirement(params SiteRole[] anyOf) : IAuthorizationRequirement
{
    public SiteRole[] AnyOf { get; } = anyOf;
}

public static class SiteRoles
{
    public static Guid? UserId(ClaimsPrincipal p)
        => Guid.TryParse(p.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public static bool IsGospodarAdmin(ClaimsPrincipal p)
        => p.FindAll(ClaimTypes.Role).Any(c => c.Value == "Admin");

    public static bool Allowed(IReadOnlyCollection<SiteRole> have, SiteRole[] anyOf)
        => have.Contains(SiteRole.Admin) || anyOf.Any(have.Contains);

    /// Effective roles for /me and the admin menu.
    public static async Task<List<SiteRole>> EffectiveAsync(ClaimsPrincipal p, AppDbContext db, CancellationToken ct)
    {
        if (IsGospodarAdmin(p)) return [SiteRole.Admin];
        var id = UserId(p);
        if (id is null) return [];
        var roles = await db.SiteUsers.Where(u => u.Id == id).Select(u => u.Roles).FirstOrDefaultAsync(ct);
        return roles ?? [];
    }
}

public sealed class SiteRoleHandler(AppDbContext db) : AuthorizationHandler<SiteRoleRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, SiteRoleRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;
        var roles = await SiteRoles.EffectiveAsync(context.User, db, CancellationToken.None);
        if (SiteRoles.Allowed(roles, requirement.AnyOf))
            context.Succeed(requirement);
    }
}
