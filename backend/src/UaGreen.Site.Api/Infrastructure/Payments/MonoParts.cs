using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Auth;

namespace UaGreen.Site.Api.Infrastructure.Payments;

public sealed class MonoPartsOptions
{
    /// Sandbox by default; prod is https://u2.monobank.com.ua.
    public string BaseUrl { get; set; } = "https://u2-demo-ext.mono.st4g3.com";
    public string StoreId { get; set; } = "";
    public string Secret { get; set; } = "";
    public bool Configured => StoreId != "" && Secret != "";
}

public sealed record MonoPartsState(
    [property: JsonPropertyName("order_id")] string OrderId,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("order_sub_state")] string SubState)
{
    public string Text => $"{State}/{SubState}";
}

public sealed class MonoPartsException(string message) : Exception(message);

/// monobank "Покупка частинами": the client confirms in the monobank app, the store confirms
/// once the goods are handed over. Every request: store-id + base64(HMAC-SHA256(secret, body)).
public sealed class MonoPartsClient(HttpClient http, IOptions<MonoPartsOptions> opts, IOptions<AppOptions> app)
{
    private readonly MonoPartsOptions _o = opts.Value;
    private readonly string _base = app.Value.PublicBaseUrl.TrimEnd('/');

    /// Returns the bank's order_id. A repeat with the same store_order_id returns the same id, so retries are safe.
    public async Task<string> CreateAsync(Order o, string phone, int maxParts, CancellationToken ct)
    {
        var body = BuildCreateBody(o, phone, maxParts, $"{_base}/api/payments/monoparts/callback", DateOnly.FromDateTime(DateTime.UtcNow));
        using var doc = await PostAsync("/api/order/create", body, ct);
        return doc.RootElement.TryGetProperty("order_id", out var id) && id.GetString() is { Length: > 0 } s
            ? s : throw new MonoPartsException("monobank не повернув order_id");
    }

    public Task<MonoPartsState> StateAsync(string orderId, CancellationToken ct) => OrderCallAsync("/api/order/state", orderId, ct);
    public Task<MonoPartsState> ConfirmAsync(string orderId, CancellationToken ct) => OrderCallAsync("/api/order/confirm", orderId, ct);
    public Task<MonoPartsState> RejectAsync(string orderId, CancellationToken ct) => OrderCallAsync("/api/order/reject", orderId, ct);

    private async Task<MonoPartsState> OrderCallAsync(string path, string orderId, CancellationToken ct)
    {
        using var doc = await PostAsync(path, JsonSerializer.SerializeToUtf8Bytes(new { order_id = orderId }), ct);
        return doc.Deserialize<MonoPartsState>() is { State: not null, SubState: not null } s
            ? s : throw new MonoPartsException("monobank повернув незрозумілий статус");
    }

    private async Task<JsonDocument> PostAsync(string path, byte[] body, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, path) { Content = new ByteArrayContent(body) };
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        req.Headers.Add("store-id", _o.StoreId);
        req.Headers.Add("signature", Sign(body, _o.Secret));
        try
        {
            using var res = await http.SendAsync(req, ct);
            var text = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode) throw new MonoPartsException(Message(text) ?? $"HTTP {(int)res.StatusCode}");
            return JsonDocument.Parse(text);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            throw new MonoPartsException(ex is TaskCanceledException ? "monobank не відповів вчасно" : ex.Message);
        }
    }

    private static string? Message(string body)
    {
        try { return JsonDocument.Parse(body).RootElement.GetProperty("message").GetString(); }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { return null; }
    }

    public static string Sign(byte[] body, string secret) =>
        Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body));

    /// The bank wants +380XXXXXXXXX; anything that is not a Ukrainian mobile/landline number → null.
    public static string? NormalizePhone(string? phone)
    {
        var d = new string((phone ?? "").Where(char.IsAsciiDigit).ToArray());
        if (d.Length == 10 && d[0] == '0') return "+38" + d;
        if (d.Length == 12 && d.StartsWith("380")) return "+" + d;
        return null;
    }

    /// Admin setting pay_parts_max: 3..25, anything else → 6.
    public static int MaxParts(string? setting) =>
        int.TryParse(setting?.Trim(), out var n) && n is >= 3 and <= 25 ? n : 6;

    public static int[] Parts(int max) => Enumerable.Range(3, Math.Max(1, max - 2)).ToArray();

    public static byte[] BuildCreateBody(Order o, string phone, int maxParts, string callbackUrl, DateOnly today) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            store_order_id = o.Id.ToString(),
            client_phone = phone,
            total_sum = o.Total,
            invoice = new { date = today.ToString("yyyy-MM-dd"), number = o.Number.ToString(), source = "INTERNET" },
            available_programs = new[] { new { available_parts_count = Parts(maxParts), type = "payment_installments" } },
            products = o.Items.Select(i => new { name = i.ProductName, count = i.Qty, sum = i.LineTotal }).ToArray(),
            result_callback = callbackUrl,
        });
}
