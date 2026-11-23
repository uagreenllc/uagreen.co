using System.Net;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Features.Stats;
using UaGreen.Site.Api.Infrastructure.Geo;

namespace UaGreen.Site.Tests;

public class StatsTests
{
    [Theory]
    [InlineData(null, "", TrafficSource.Direct)]
    [InlineData("https://www.google.com.ua/", "", TrafficSource.Search)]
    [InlineData("https://mail.google.com/", "", TrafficSource.Email)]
    [InlineData("https://l.facebook.com/l.php", "", TrafficSource.Social)]
    [InlineData("https://t.co/abc", "", TrafficSource.Social)]
    [InlineData("https://www.microsoft.com/", "", TrafficSource.Referral)] // "t.co" must not match the tail of "microsoft.com"
    [InlineData("android-app://org.telegram.messenger/", "", TrafficSource.Other)]
    [InlineData("https://www.google.com/", "?gclid=xyz", TrafficSource.Ads)]
    [InlineData(null, "?utm_source=newsletter&utm_medium=email", TrafficSource.Email)]
    [InlineData(null, "?utm_source=telegram", TrafficSource.Social)]
    [InlineData(null, "?utm_source=partner42", TrafficSource.Other)]
    public void Classifies_traffic_source(string? referrer, string query, TrafficSource expected)
        => Assert.Equal(expected, Traffic.Classify(referrer, query));

    [Fact]
    public void Finds_country_for_ipv4_and_ipv6()
    {
        var t = GeoTable.Load(new StringReader("""
            1.0.0.0,1.0.0.255,AU
            1.0.1.0,1.0.3.255,CN
            1.0.4.0,1.0.4.255,ZZ
            2001:200::,2001:200:ffff:ffff:ffff:ffff:ffff:ffff,JP
            """));
        Assert.Equal("AU", t.Find(IPAddress.Parse("1.0.0.7")));
        Assert.Equal("CN", t.Find(IPAddress.Parse("1.0.2.5")));
        Assert.Equal("AU", t.Find(IPAddress.Parse("::ffff:1.0.0.7"))); // IPv4 behind a dual-stack socket
        Assert.Equal("JP", t.Find(IPAddress.Parse("2001:200::1")));
        Assert.Null(t.Find(IPAddress.Parse("1.0.4.1"))); // ZZ = unknown
        Assert.Null(t.Find(IPAddress.Parse("9.9.9.9")));
        Assert.Null(t.Find(IPAddress.Parse("0.0.0.1")));
    }
}
