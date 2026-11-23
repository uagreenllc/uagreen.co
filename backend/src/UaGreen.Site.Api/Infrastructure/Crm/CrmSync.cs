using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Persistence;

namespace UaGreen.Site.Api.Infrastructure.Crm;

/// Body of Gospodar's POST /api/integration/leads (`source` names the deal source in the CRM).
public sealed record CrmLead(string ExternalId, string? Name, string? Phone, string? Email, string? Comment, string? ProductInterest, string Source);

public sealed record CrmResult(Guid? DealId, string? Error);

/// Sends contact requests and orders to Gospodar CRM as leads.
/// Contact requests: on whenever the key is set (`admin.crm.contacts` = "off" pauses them),
/// the unsent ones of the last 3 days. Orders: opt-in — `admin.crm.orders` holds the ISO
/// time the feed was switched on, and only orders since then go (no flood of history).
public sealed class CrmSync(IHttpClientFactory http, IOptions<GospodarOptions> opts, ILogger<CrmSync> log)
{
    public const string OrdersKey = "admin.crm.orders", ContactsKey = "admin.crm.contacts";
    public const string Source = "Сайт uagreen.co";
    public const int MaxAttempts = 20;

    /// ponytail: a request unsent for 3 days stays only in the site inbox + email — resend it by hand.
    public static readonly TimeSpan ContactsWindow = TimeSpan.FromDays(3);

    private readonly GospodarOptions _o = opts.Value;

    public bool Configured => !string.IsNullOrWhiteSpace(_o.IntegrationKey);

    public Task PushAsync(Order o, CancellationToken ct) => PushAsync(o, LeadFor(o), ct);
    public Task PushAsync(ContactRequest c, CancellationToken ct) => PushAsync(c, LeadFor(c), ct);

    private async Task PushAsync(ICrmLinked row, CrmLead lead, CancellationToken ct)
    {
        if (row.CrmDealId is not null) return;
        var r = Configured ? await SendAsync(lead, ct) : new CrmResult(null, "Не задано ключ інтеграції Господаря (Gospodar__IntegrationKey)");
        row.CrmAttempts++;
        row.CrmLastAttemptAt = DateTime.UtcNow;
        row.CrmDealId = r.DealId;
        row.CrmError = r.Error is { Length: > 500 } e ? e[..500] : r.Error;
        if (r.DealId is not null) row.CrmSyncedAt = DateTime.UtcNow;
    }

    private async Task<CrmResult> SendAsync(CrmLead lead, CancellationToken ct)
    {
        try
        {
            var body0 = JsonContent.Create(lead);
            await body0.LoadIntoBufferAsync(ct); // Content-Length instead of chunked: proxies in front of Gospodar
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/integration/leads") { Content = body0 };
            req.Headers.Add("X-Api-Key", _o.IntegrationKey);
            using var resp = await Client().SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (resp.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(body);
                return new CrmResult(doc.RootElement.GetProperty("dealId").GetGuid(), null);
            }
            return new CrmResult(null, resp.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Господар не прийняв ключ інтеграції — перевірте Gospodar__IntegrationKey",
                HttpStatusCode.ServiceUnavailable when body.Contains("no_responsible") => "У Господарі нема менеджера, на якого поставити угоду",
                _ => $"Господар відповів {(int)resp.StatusCode}: {body}",
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        {
            log.LogWarning(ex, "CRM push {Id} failed", lead.ExternalId);
            return new CrmResult(null, "Господар недоступний: " + ex.Message);
        }
    }

    /// Read-only call with the key — 200 means the key works. Null = OK, else the reason.
    public async Task<string?> TestAsync(CancellationToken ct)
    {
        if (!Configured) return "Ключ інтеграції не задано (Gospodar__IntegrationKey у .env сервера)";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/api/integration/orders?take=1");
            req.Headers.Add("X-Api-Key", _o.IntegrationKey);
            using var resp = await Client().SendAsync(req, ct);
            return resp.IsSuccessStatusCode ? null
                : resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? "Господар не прийняв ключ"
                : $"Господар відповів {(int)resp.StatusCode}";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return "Господар недоступний: " + ex.Message;
        }
    }

    private HttpClient Client()
    {
        var c = http.CreateClient(nameof(CrmSync));
        c.BaseAddress = new Uri(_o.BaseUrl);
        return c;
    }

    /// Due for an automatic retry: 1, 2, 4 … minutes after the last try, capped at 6 h.
    public static bool Due(ICrmLinked r, DateTime now)
        => r.CrmDealId is null && r.CrmAttempts < MaxAttempts
           && (r.CrmLastAttemptAt is not { } last || last.AddMinutes(Math.Min(360, Math.Pow(2, r.CrmAttempts - 1))) <= now);

    public static DateTime? Since(string? setting)
        => DateTime.TryParse(setting, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d) ? d : null;

    public static CrmLead LeadFor(Order o)
    {
        var lines = new List<string> { $"Замовлення №{o.Number} з сайту uagreen.co" };
        lines.AddRange(o.Items.Select(i => $"• {i.ProductName}{(i.OptionsText is null ? "" : $" ({i.OptionsText})")} — {i.Qty} × {i.UnitPrice:0.##} = {i.LineTotal:0.##} грн"));
        lines.Add($"Разом: {o.Total:0.##} грн");
        lines.Add("Оплата: " + o.PaymentMethod switch { PaymentMethod.LiqPay => "LiqPay (картка)", PaymentMethod.Invoice => "переказ на IBAN", _ => "кредит 0%" });
        lines.Add("Доставка: " + string.Join(", ", new[]
        {
            o.DeliveryMethod switch { DeliveryMethod.NovaPoshta => "Нова пошта", DeliveryMethod.Ukrposhta => "Укрпошта", _ => "самовивіз" },
            o.DeliveryCity, o.DeliveryWarehouse,
        }.Where(s => !string.IsNullOrWhiteSpace(s))));
        if (!string.IsNullOrWhiteSpace(o.Comment)) lines.Add("Коментар: " + o.Comment);
        var interest = string.Join(", ", o.Items.Select(i => i.ProductName).Distinct());
        return new CrmLead($"site-order-{o.Number}", Cut(o.CustomerName, 200), Cut(o.Phone, 40), Cut(o.Email, 200),
            Cut(string.Join('\n', lines), 1000), Cut(interest, 200), Source);
    }

    /// ExternalId = the bare request id, as the first CRM push used: a retry never doubles a deal.
    /// The message as is — audit consultations come through ContactRequest too.
    public static CrmLead LeadFor(ContactRequest c)
        => new(c.Id.ToString("N"), Cut(c.Name, 200), Cut(c.Phone, 40), Cut(c.Email, 200), Cut(c.Message, 1000), null, Source);

    private static string? Cut(string? s, int max) => s is null ? null : s.Length > max ? s[..(max - 1)] + "…" : s;
}

/// Minute loop: sends what's new and retries failures with backoff.
public sealed class CrmSyncWorker(IServiceScopeFactory scopes, ILogger<CrmSyncWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try { await TickAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogError(ex, "CRM sync tick failed"); }
        } while (await timer.WaitForNextTickAsync(ct));
    }

    private async Task TickAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        await TickAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), scope.ServiceProvider.GetRequiredService<CrmSync>(), ct);
    }

    public static async Task TickAsync(AppDbContext db, CrmSync sync, CancellationToken ct)
    {
        if (!sync.Configured) return;
        var settings = await db.Settings.AsNoTracking().Where(s => s.Key == CrmSync.OrdersKey || s.Key == CrmSync.ContactsKey)
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);
        var now = DateTime.UtcNow;

        // ponytail: candidates filtered for backoff in memory — the unsent set is tiny; move Due() into SQL if it ever isn't.
        if (CrmSync.Since(settings.GetValueOrDefault(CrmSync.OrdersKey)) is { } os)
            foreach (var o in (await db.Orders.Include(x => x.Items)
                         .Where(x => x.CrmDealId == null && x.CrmAttempts < CrmSync.MaxAttempts && x.CreatedAt >= os)
                         .OrderBy(x => x.CreatedAt).Take(50).ToListAsync(ct)).Where(x => CrmSync.Due(x, now)))
                await sync.PushAsync(o, ct);

        var contactsFrom = now - CrmSync.ContactsWindow;
        if (settings.GetValueOrDefault(CrmSync.ContactsKey) != "off")
            foreach (var c in (await db.ContactRequests
                         .Where(x => x.CrmDealId == null && x.CrmAttempts < CrmSync.MaxAttempts && x.CreatedAt > contactsFrom)
                         .OrderBy(x => x.CreatedAt).Take(50).ToListAsync(ct)).Where(x => CrmSync.Due(x, now)))
                await sync.PushAsync(c, ct);

        await db.SaveChangesAsync(ct);
    }
}
