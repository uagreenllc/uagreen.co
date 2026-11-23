using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Persistence;

namespace UaGreen.Site.Api.Features.Forms;

/// Contact-form requests → leads in Gospodar's CRM, source «Сайт uagreen.co».
/// A loop, not an inline call: Gospodar may be down or have no free manager (503),
/// and the lead must still land once it is back. ExternalId = request id, so a retry
/// never doubles the deal.
public sealed class CrmLeadSync(IServiceScopeFactory scopes, IOptions<GospodarOptions> options, ILogger<CrmLeadSync> log)
    : BackgroundService
{
    public const string Source = "Сайт uagreen.co";
    // ponytail: gives up after 3 days — a longer Gospodar outage leaves the request only in the site inbox + email.
    private static readonly TimeSpan GiveUpAfter = TimeSpan.FromDays(3);

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        if (string.IsNullOrWhiteSpace(options.Value.IntegrationKey))
        {
            log.LogInformation("Gospodar:IntegrationKey is not set — contact requests are not sent to the CRM.");
            return;
        }
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            // An escaped exception would stop the whole host (BackgroundService default).
            try
            {
                using var scope = scopes.CreateScope();
                await PushAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(),
                    scope.ServiceProvider.GetRequiredService<GospodarClient>(), log, stop);
            }
            catch (Exception ex) when (!stop.IsCancellationRequested) { log.LogError(ex, "CRM lead sync failed."); }
        }
        while (await timer.WaitForNextTickAsync(stop));
    }

    public static async Task PushAsync(AppDbContext db, GospodarClient gospodar, ILogger log, CancellationToken ct)
    {
        var since = DateTime.UtcNow - GiveUpAfter;
        var pending = await db.ContactRequests
            .Where(c => c.CrmDealId == null && c.CreatedAt > since)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);
        foreach (var c in pending)
        {
            try
            {
                c.CrmDealId = await gospodar.CreateLeadAsync(new GospodarLead(
                    c.Id.ToString("N"), c.Name, c.Phone, c.Email,
                    c.Message.Length > 1000 ? c.Message[..999] + "…" : c.Message, // Gospodar's comment limit
                    Source), ct);
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested) // HttpClient timeouts are TaskCanceledException too
            {
                log.LogWarning(ex, "Contact request {Id} not sent to the CRM yet; retrying next minute.", c.Id);
            }
        }
    }
}
