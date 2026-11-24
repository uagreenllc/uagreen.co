using System.Text;
using System.Text.Json;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Payments;

namespace UaGreen.Site.Tests;

public class MonoPartsTests
{
    // Vector computed independently with Python: base64(hmac_sha256(secret, body)); the sandbox accepted it.
    [Fact]
    public void Sign_matches_python_vector() => Assert.Equal("3gXVwnp3FqizzUICf5cwhp7PGVBqt+vGeLeHKS3dRfg=",
        MonoPartsClient.Sign(Encoding.UTF8.GetBytes("{\"order_id\":\"a0eebc99-9c0b-4ef8-bb6d-000000000004\"}"), "secret_98765432--123-123"));

    [Theory]
    [InlineData("0501234561", "+380501234561")]
    [InlineData("+38 (050) 123-45-61", "+380501234561")]
    [InlineData("380501234561", "+380501234561")]
    [InlineData("050 123 45 61", "+380501234561")]
    [InlineData("(050)1234561", "+380501234561")]
    [InlineData("12345", null)]
    [InlineData("+48501234561", null)]
    [InlineData(null, null)]
    public void NormalizePhone(string? input, string? expected) => Assert.Equal(expected, MonoPartsClient.NormalizePhone(input));

    [Theory]
    [InlineData(null, 6)]
    [InlineData("", 6)]
    [InlineData("abc", 6)]
    [InlineData("2", 6)]
    [InlineData("3", 3)]
    [InlineData("25", 25)]
    public void MaxParts(string? s, int expected) => Assert.Equal(expected, MonoPartsClient.MaxParts(s));

    [Fact]
    public void Parts_from_three_to_max() => Assert.Equal([3, 4, 5, 6], MonoPartsClient.Parts(6));

    [Fact]
    public void BuildCreateBody_has_bank_fields()
    {
        var o = new Order
        {
            Id = Guid.NewGuid(), Number = 1001, Total = 14999.99m,
            Items = [new OrderItem { ProductName = "К5", Qty = 1, UnitPrice = 14999.99m, LineTotal = 14999.99m }],
        };

        var body = MonoPartsClient.BuildCreateBody(o, "+380501234561", 4,
            "https://uagreen.co/api/payments/monoparts/callback", new DateOnly(2026, 11, 24));

        var j = JsonDocument.Parse(body).RootElement;
        Assert.Equal(o.Id.ToString(), j.GetProperty("store_order_id").GetString());
        Assert.Equal("+380501234561", j.GetProperty("client_phone").GetString());
        Assert.Equal(14999.99m, j.GetProperty("total_sum").GetDecimal());
        var inv = j.GetProperty("invoice");
        Assert.Equal("2026-11-24", inv.GetProperty("date").GetString());
        Assert.Equal("1001", inv.GetProperty("number").GetString());
        Assert.Equal("INTERNET", inv.GetProperty("source").GetString());
        var prog = j.GetProperty("available_programs")[0];
        Assert.Equal([3, 4], prog.GetProperty("available_parts_count").EnumerateArray().Select(e => e.GetInt32()));
        Assert.Equal("payment_installments", prog.GetProperty("type").GetString());
        var p = j.GetProperty("products")[0];
        Assert.Equal("К5", p.GetProperty("name").GetString());
        Assert.Equal(1, p.GetProperty("count").GetInt32());
        Assert.Equal(14999.99m, p.GetProperty("sum").GetDecimal());
        Assert.Equal("https://uagreen.co/api/payments/monoparts/callback", j.GetProperty("result_callback").GetString());
    }
}
