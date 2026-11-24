using System.Globalization;
using System.Text.Json;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Payments;

namespace UaGreen.Site.Tests;

public class MonobankAcquiringTests
{
    [Theory]
    [InlineData("14999.99", 1499999)]
    [InlineData("15000", 1500000)]
    [InlineData("0.005", 1)]
    public void Kopecks(string uah, long expected) =>
        Assert.Equal(expected, MonobankAcquiring.Kopecks(decimal.Parse(uah, CultureInfo.InvariantCulture)));

    [Fact]
    public void BuildInvoice_fields()
    {
        var o = new Order { Id = Guid.NewGuid(), Number = 42, Total = 15000m };

        var j = JsonDocument.Parse(JsonSerializer.Serialize(MonobankAcquiring.BuildInvoice(o, "https://uagreen.co/"))).RootElement;

        Assert.Equal(1500000, j.GetProperty("amount").GetInt64());
        Assert.Equal(980, j.GetProperty("ccy").GetInt32());
        var info = j.GetProperty("merchantPaymInfo");
        Assert.Equal(o.Id.ToString(), info.GetProperty("reference").GetString());
        Assert.Equal("UA GREEN, замовлення №42", info.GetProperty("destination").GetString());
        Assert.False(info.TryGetProperty("basketOrder", out _));
        Assert.Equal($"https://uagreen.co/order/{o.Id}", j.GetProperty("redirectUrl").GetString());
        Assert.Equal("https://uagreen.co/api/payments/monobank/webhook", j.GetProperty("webHookUrl").GetString());
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("created", true)]
    [InlineData("processing", true)]
    [InlineData("failure", false)]
    [InlineData("expired", false)]
    [InlineData("reversed", false)]
    [InlineData("success", false)]
    public void Reusable(string? s, bool expected) => Assert.Equal(expected, MonobankAcquiring.Reusable(s));
}
