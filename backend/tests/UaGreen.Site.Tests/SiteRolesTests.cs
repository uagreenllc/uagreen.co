using System.Security.Claims;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Auth;

namespace UaGreen.Site.Tests;

public class SiteRolesTests
{
    [Fact]
    public void Policy_matrix()
    {
        SiteRole[] sales = [SiteRole.Sales], hr = [SiteRole.Hr], salesOrEditor = [SiteRole.Sales, SiteRole.Editor];

        Assert.True(SiteRoles.Allowed([SiteRole.Sales], sales));
        Assert.False(SiteRoles.Allowed([SiteRole.Hr], sales));        // HR cannot touch products
        Assert.False(SiteRoles.Allowed([SiteRole.Sales], hr));        // Sales cannot touch vacancies
        Assert.True(SiteRoles.Allowed([SiteRole.Editor], salesOrEditor));
        Assert.False(SiteRoles.Allowed([SiteRole.Hr], salesOrEditor));
        Assert.True(SiteRoles.Allowed([SiteRole.Admin], hr));         // Admin everywhere
        Assert.False(SiteRoles.Allowed([], sales));
    }

    [Fact]
    public void Gospodar_admin_claim_is_recognised()
    {
        var admin = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Worker"), new Claim(ClaimTypes.Role, "Admin")], "test"));
        var worker = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Worker")], "test"));
        Assert.True(SiteRoles.IsGospodarAdmin(admin));
        Assert.False(SiteRoles.IsGospodarAdmin(worker));
    }
}
