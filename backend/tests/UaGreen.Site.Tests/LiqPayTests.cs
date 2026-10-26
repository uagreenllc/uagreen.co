using Microsoft.Extensions.Options;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Payments;

namespace UaGreen.Site.Tests;

public class LiqPayTests
{
    // Vector computed independently with Python: base64(sha1(priv + data + priv)).
    private const string Priv = "sandbox_test_private";
    private const string Data = "eyJ2ZXJzaW9uIjozLCJhY3Rpb24iOiJwYXkiLCJhbW91bnQiOjQ3MDAwLCJjdXJyZW5jeSI6IlVBSCIsIm9yZGVyX2lkIjoiYWJjIn0=";
    private const string Sig = "PB0QRAe3Bmh3tiTdgrYZjZnYO3E=";

    private static LiqPayService Svc() => new(
        Options.Create(new LiqPayOptions { PublicKey = "sandbox_pub", PrivateKey = Priv, Sandbox = true }),
        Options.Create(new AppOptions { PublicBaseUrl = "https://uagreen.co/" }));

    [Fact]
    public void Sign_matches_known_vector() => Assert.Equal(Sig, LiqPayService.Sign(Data, Priv));

    [Fact]
    public void VerifySignature_accepts_valid_and_rejects_tampered()
    {
        var svc = Svc();
        Assert.True(svc.VerifySignature(Data, Sig));
        Assert.False(svc.VerifySignature(Data, "PB0QRAe3Bmh3tiTdgrYZjZnYO3F="));
        Assert.False(svc.VerifySignature(Data + "x", Sig));
        Assert.False(svc.VerifySignature(Data, ""));
    }

    [Fact]
    public void BuildCheckout_roundtrips_order_and_amount()
    {
        var order = new Order { Id = Guid.NewGuid(), Number = 42, Total = 47000m };
        var (data, signature) = Svc().BuildCheckout(order, "uk");
        Assert.True(Svc().VerifySignature(data, signature));
        var cb = LiqPayService.Decode(data)!;
        Assert.Equal(order.Id.ToString(), cb.OrderId);
        Assert.Equal(47000m, cb.Amount);
        Assert.Equal("UAH", cb.Currency);
        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(data));
        Assert.Contains("\"result_url\":\"https://uagreen.co/order/" + order.Id, json);
        Assert.Contains("\"server_url\":\"https://uagreen.co/api/payments/liqpay/callback\"", json);
        Assert.Contains("\"sandbox\":1", json);
    }

    [Theory]
    [InlineData("success", true)]
    [InlineData("sandbox", true)]
    [InlineData("wait_accept", true)]
    [InlineData("failure", false)]
    [InlineData("error", false)]
    [InlineData(null, false)]
    public void IsPaid(string? status, bool expected) => Assert.Equal(expected, LiqPayService.IsPaid(status));
}
