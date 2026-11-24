using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Crm;
using UaGreen.Site.Api.Infrastructure.Persistence;

namespace UaGreen.Site.Api.Features.Admin;

public sealed record CrmFeedsUpdate(bool Orders, bool Contacts);

/// Dashboard numbers + menu badges (one call, sections by role) and the Gospodar
/// integration screen (Admin).
public static class AdminDashboardEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/summary", async (HttpContext http, AppDbContext db, IOptions<GospodarOptions> gospodar,
            IOptions<AppOptions> site, CancellationToken ct) =>
        {
            var roles = await SiteRoles.EffectiveAsync(http.User, db, ct);
            bool Has(params SiteRole[] r) => SiteRoles.Allowed(roles, r);
            var monthAgo = DateTime.UtcNow.AddDays(-30);
            var paid = new[] { OrderStatus.Paid, OrderStatus.Shipped, OrderStatus.InvoiceSent, OrderStatus.Credit };

            object? orders = null, products = null, contacts = null, applications = null;
            if (Has(SiteRole.Sales))
            {
                var recent = await db.Orders.AsNoTracking().OrderByDescending(o => o.CreatedAt).Take(6)
                    .Select(o => new { o.Id, o.Number, o.CustomerName, o.Total, Status = o.Status.ToString(), o.CreatedAt }).ToListAsync(ct);
                orders = new
                {
                    New = await db.Orders.CountAsync(o => o.Status == OrderStatus.New, ct),
                    AwaitingPayment = await db.Orders.CountAsync(o => o.Status == OrderStatus.AwaitingPayment, ct),
                    Count30d = await db.Orders.CountAsync(o => o.CreatedAt >= monthAgo && o.Status != OrderStatus.Cancelled, ct),
                    Revenue30d = await db.Orders.Where(o => o.CreatedAt >= monthAgo && paid.Contains(o.Status)).SumAsync(o => o.Total, ct),
                    CrmFailed = await db.Orders.CountAsync(o => o.CrmDealId == null && o.CrmError != null, ct),
                    Recent = recent,
                };
                products = new
                {
                    Total = await db.Products.CountAsync(ct),
                    Drafts = await db.Products.CountAsync(p => !p.Published, ct),
                    NoPhoto = await db.Products.CountAsync(p => !p.Images.Any(), ct),
                    OutOfStock = await db.Products.CountAsync(p => !p.InStock, ct),
                };
            }
            if (Has(SiteRole.Sales, SiteRole.Editor))
                contacts = new
                {
                    Unread = await db.ContactRequests.CountAsync(c => !c.IsRead, ct),
                    CrmFailed = await db.ContactRequests.CountAsync(c => c.CrmDealId == null && c.CrmError != null, ct),
                };
            if (Has(SiteRole.Hr))
                applications = new
                {
                    Undelivered = await db.Applications.CountAsync(a => a.GospodarCandidateId == null, ct),
                };

            var oberig = await db.Settings.AsNoTracking().Where(s => s.Key == "oberig.url").Select(s => s.Value).FirstOrDefaultAsync(ct);
            return new
            {
                orders, products, contacts, applications,
                Links = new
                {
                    Site = site.Value.PublicBaseUrl.TrimEnd('/'),
                    Erp = (gospodar.Value.WebUrl ?? gospodar.Value.BaseUrl).TrimEnd('/'),
                    Oberig = (oberig ?? "https://portal.uagreen.co").TrimEnd('/'),
                },
            };
        }).RequireAuthorization();

        var g = app.MapGroup("/api/admin/integrations").RequireAuthorization(Policies.Admin);

        g.MapGet("/gospodar", async (AppDbContext db, CrmSync sync, IOptions<GospodarOptions> gospodar, CancellationToken ct) =>
        {
            var s = await db.Settings.AsNoTracking().Where(x => x.Key == CrmSync.OrdersKey || x.Key == CrmSync.ContactsKey)
                .ToDictionaryAsync(x => x.Key, x => x.Value, ct);
            return new
            {
                Configured = sync.Configured,
                gospodar.Value.BaseUrl,
                OrdersSince = CrmSync.Since(s.GetValueOrDefault(CrmSync.OrdersKey)),
                ContactsOn = s.GetValueOrDefault(CrmSync.ContactsKey) != "off",
                Orders = new
                {
                    Sent = await db.Orders.CountAsync(o => o.CrmDealId != null, ct),
                    Failed = await db.Orders.CountAsync(o => o.CrmDealId == null && o.CrmError != null, ct),
                },
                Contacts = new
                {
                    Sent = await db.ContactRequests.CountAsync(c => c.CrmDealId != null, ct),
                    Failed = await db.ContactRequests.CountAsync(c => c.CrmDealId == null && c.CrmError != null, ct),
                },
            };
        });

        g.MapPost("/gospodar/test", async (CrmSync sync, CancellationToken ct) =>
        {
            var error = await sync.TestAsync(ct);
            return new { Ok = error is null, Error = error };
        });

        // Orders on stamps "since = now" (history is not replayed into CRM); contacts are on
        // by default and only an explicit "off" row pauses them.
        g.MapPut("/gospodar", async (CrmFeedsUpdate req, AppDbContext db, CancellationToken ct) =>
        {
            var orders = await db.Settings.FirstOrDefaultAsync(x => x.Key == CrmSync.OrdersKey, ct);
            if (!req.Orders) { if (orders is not null) db.Settings.Remove(orders); }
            else if (orders is null) db.Settings.Add(new Setting { Key = CrmSync.OrdersKey, Value = DateTime.UtcNow.ToString("O") });

            var contacts = await db.Settings.FirstOrDefaultAsync(x => x.Key == CrmSync.ContactsKey, ct);
            if (req.Contacts) { if (contacts is not null) db.Settings.Remove(contacts); }
            else if (contacts is null) db.Settings.Add(new Setting { Key = CrmSync.ContactsKey, Value = "off" });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }
}
