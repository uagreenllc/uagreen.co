using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Auth;

namespace UaGreen.Site.Api.Infrastructure.Payments;

public sealed class LiqPayOptions
{
    public string PublicKey { get; set; } = "";
    public string PrivateKey { get; set; } = "";
    public bool Sandbox { get; set; } = true;
}

/// Decoded `data` of a LiqPay server callback (only the fields we act on).
public sealed record LiqPayCallback(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("order_id")] string? OrderId,
    [property: JsonPropertyName("amount")] decimal? Amount,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("payment_id")] long? PaymentId,
    [property: JsonPropertyName("err_description")] string? ErrDescription);

/// LiqPay Checkout API v3: data = base64(json), signature = base64(sha1(private + data + private)).
public sealed class LiqPayService(IOptions<LiqPayOptions> opts, IOptions<AppOptions> app)
{
    public const string CheckoutUrl = "https://www.liqpay.ua/api/3/checkout";
    private readonly LiqPayOptions _o = opts.Value;
    private readonly string _base = app.Value.PublicBaseUrl.TrimEnd('/');

    public (string Data, string Signature) BuildCheckout(Order order, string lang)
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["version"] = 3,
            ["public_key"] = _o.PublicKey,
            ["action"] = "pay",
            ["amount"] = order.Total,
            ["currency"] = "UAH",
            ["description"] = $"UA GREEN, замовлення №{order.Number}",
            ["order_id"] = order.Id.ToString(),
            ["result_url"] = $"{_base}/order/{order.Id}",
            ["server_url"] = $"{_base}/api/payments/liqpay/callback",
            ["language"] = lang == "en" ? "en" : "uk",
            ["sandbox"] = _o.Sandbox ? 1 : 0,
        });
        var data = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        return (data, Sign(data, _o.PrivateKey));
    }

    public bool VerifySignature(string data, string signature)
    {
        var expected = Encoding.UTF8.GetBytes(Sign(data, _o.PrivateKey));
        var actual = Encoding.UTF8.GetBytes(signature ?? "");
        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    public static LiqPayCallback? Decode(string data)
    {
        try
        {
            return JsonSerializer.Deserialize<LiqPayCallback>(Convert.FromBase64String(data));
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }

    /// `success` is a real payment; `sandbox` is the test-mode equivalent. `wait_accept`
    /// means the money is on its way but the shop is not yet verified by LiqPay — treat as paid.
    public static bool IsPaid(string? status) => status is "success" or "sandbox" or "wait_accept";

    public static string Sign(string data, string privateKey)
        => Convert.ToBase64String(SHA1.HashData(Encoding.UTF8.GetBytes(privateKey + data + privateKey)));
}
