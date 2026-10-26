using Microsoft.EntityFrameworkCore;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Persistence;

namespace UaGreen.Site.Api.Features.Admin;

public sealed record UserAdminDto(Guid Id, string Email, string FullName, bool IsActive, bool GospodarAdmin, List<SiteRole> Roles);
public sealed record RolesUpdate(List<SiteRole> Roles, string? Email, string? FullName);

/// Role assignment. Users come from Gospodar; roles live here. Admin only.
public static class AdminUsersEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin/users").RequireAuthorization(Policies.Admin);

        g.MapGet("/", async (GospodarClient gospodar, AppDbContext db, CancellationToken ct) =>
        {
            var local = await db.SiteUsers.AsNoTracking().ToDictionaryAsync(u => u.Id, ct);
            var remote = await gospodar.GetUsersAsync(ct);
            if (remote is null)
                // Gospodar unreachable: show who already has roles so the screen still works.
                return Results.Ok(new { gospodarAvailable = false, users = local.Values.Select(u => new UserAdminDto(u.Id, u.Email, u.FullName, true, false, u.Roles)) });

            var users = remote.Where(u => u.IsActive).Select(u => new UserAdminDto(
                u.Id, u.Email ?? local.GetValueOrDefault(u.Id)?.Email ?? "", u.FullName ?? local.GetValueOrDefault(u.Id)?.FullName ?? "",
                u.IsActive, u.Roles?.Contains("Admin") == true, local.GetValueOrDefault(u.Id)?.Roles ?? []))
                .OrderBy(u => u.FullName).ToList();
            return Results.Ok(new { gospodarAvailable = true, users });
        });

        g.MapPut("/{id:guid}/roles", async (Guid id, RolesUpdate req, GospodarClient gospodar, AppDbContext db, CancellationToken ct) =>
        {
            var roles = (req.Roles ?? []).Distinct().ToList();
            var row = await db.SiteUsers.FirstOrDefaultAsync(u => u.Id == id, ct);
            if (row is null)
            {
                var remote = (await gospodar.GetUsersAsync(ct))?.FirstOrDefault(u => u.Id == id);
                if (remote is null && string.IsNullOrWhiteSpace(req.Email)) throw ProblemException.NotFound("User not found in Gospodar");
                row = new SiteUser { Id = id, Email = remote?.Email ?? req.Email ?? "", FullName = remote?.FullName ?? req.FullName ?? "" };
                db.SiteUsers.Add(row);
            }
            row.Roles = roles;
            row.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new UserAdminDto(row.Id, row.Email, row.FullName, true, false, row.Roles));
        });
    }
}
