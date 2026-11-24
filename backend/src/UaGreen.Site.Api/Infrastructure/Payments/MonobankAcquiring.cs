using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Auth;

namespace UaGreen.Site.Api.Infrastructure.Payments;

public sealed class MonobankOptions
{
    /// X-Token of internet acquiring: web.monobank.ua for prod, api.monobank.ua for a test token
    /// (a test token accepts any Luhn-valid card as paid).
    public string Token { get; set; } = "";
    public bool Configured => Token != "";
}

public sealed record MonoInvoice(string InvoiceId, string PageUrl);
public sealed record MonoInvoiceStatus(string InvoiceId, string Status, long Amount, int Ccy);

public sealed class MonobankException(string message) : Exception(message);

/// monobank internet acquiring: an invoice is a hosted payment page (card, Apple Pay, Google Pay).
public sealed class MonobankAcquiring(HttpClient http, IOptions<MonobankOptions> opts, IOptions<AppOptions> app)
{
    public const string BaseUrl = "https://api.monobank.ua";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string _token = opts.Value.Token;
    private readonly string _base = app.Value.PublicBaseUrl;

    public Task<MonoInvoice> CreateInvoiceAsync(Order o, CancellationToken ct) =>
        SendAsync<MonoInvoice>(new HttpRequestMessage(HttpMethod.Post, "/api/merchant/invoice/create")
            { Content = JsonContent.Create(BuildInvoice(o, _base), options: Json) }, ct);

    public Task<MonoInvoiceStatus> GetStatusAsync(string invoiceId, CancellationToken ct) =>
        SendAsync<MonoInvoiceStatus>(new HttpRequestMessage(HttpMethod.Get,
            $"/api/merchant/invoice/status?invoiceId={Uri.EscapeDataString(invoiceId)}"), ct);

    private async Task<T> SendAsync<T>(HttpRequestMessage req, CancellationToken ct) where T : class
    {
        using var _ = req;
        req.Headers.Add("X-Token", _token);
        try
        {
            using var res = await http.SendAsync(req, ct);
            var text = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode) throw new MonobankException(ErrText(text) ?? $"HTTP {(int)res.StatusCode}");
            return JsonSerializer.Deserialize<T>(text, Json) ?? throw new MonobankException("monobank повернув порожню відповідь");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            throw new MonobankException(ex is TaskCanceledException ? "monobank не відповів вчасно" : ex.Message);
        }
    }

    private static string? ErrText(string body)
    {
        try { return JsonDocument.Parse(body).RootElement.GetProperty("errText").GetString(); }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { return null; }
    }

    public static long Kopecks(decimal uah) => (long)Math.Round(uah * 100, MidpointRounding.AwayFromZero);

    // ponytail: no basketOrder — monobank requires it only with ПРРО; add the item list when fiscalization is switched on in the cabinet.
    public static object BuildInvoice(Order o, string baseUrl)
    {
        var b = baseUrl.TrimEnd('/');
        return new
        {
            amount = Kopecks(o.Total),
            ccy = 980,
            merchantPaymInfo = new { reference = o.Id.ToString(), destination = $"UA GREEN, замовлення №{o.Number}" },
            redirectUrl = $"{b}/order/{o.Id}",
            webHookUrl = $"{b}/api/payments/monobank/webhook",
        };
    }

    /// The hosted page of an invoice in these states can still take the payment; otherwise make a new invoice.
    public static bool Reusable(string? status) => status is null or "created" or "processing";
}
