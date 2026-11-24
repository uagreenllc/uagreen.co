using System.Net;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Features.Orders;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Payments;
using UaGreen.Site.Api.Infrastructure.Persistence;

namespace UaGreen.Site.Tests;

public class PaymentSettingsTests
{
    [Theory]
    [InlineData("UA213223130000026007233566001", true)]
    [InlineData("GB82WEST12345698765432", true)]
    [InlineData("UA213223130000026007233566002", false)] // one digit off
    [InlineData("UA21322313000002600723356600", false)]  // UA must be 29 chars
    [InlineData("ua213223130000026007233566001", false)] // normalised by FromGospodar, not here
    public void IsValidIban(string iban, bool ok) => Assert.Equal(ok, PaymentSettings.IsValidIban(iban));

    private static readonly Dictionary<string, string> Stored = new() { ["pay_Card"] = "off", ["pay_iban"] = "UA213223130000026007233566001" };

    [Fact]
    public void Validate_takes_a_partial_change()
    {
        var req = new Dictionary<string, string?> { ["pay_purpose"] = "Замовлення {n}" };
        PaymentSettings.Validate(req, Stored);
        Assert.Single(req); // nothing else gets cleared
    }

    [Fact]
    public void Validate_rejects_bad_input()
    {
        void Bad(Dictionary<string, string?> req) => Assert.Throws<ProblemException>(() => PaymentSettings.Validate(req, Stored));
        Bad(new() { ["pay_iban"] = "UA213223130000026007233566001" }); // Gospodar's, not the admin's
        Bad(new() { ["pay_Card"] = "on" });
        Bad(new() { ["code_head"] = "<script>" });
        Bad(new() { ["pay_Invoice"] = "off", ["pay_Credit"] = "off", ["pay_MonoParts"] = "off" }); // Card is already off
        Bad(new() { ["pay_parts_max"] = "2" });
        Bad(new() { ["pay_parts_max"] = "26" });
        Bad(new() { ["pay_parts_max"] = "abc" });
    }

    [Fact]
    public void Parts_max_is_trimmed()
    {
        var req = new Dictionary<string, string?> { ["pay_parts_max"] = " 10 " };
        PaymentSettings.Validate(req, Stored);
        Assert.Equal("10", req["pay_parts_max"]);
    }

    [Fact]
    public void Unconfigured_lists_methods_without_keys() =>
        Assert.Equal([PaymentMethod.Card, PaymentMethod.MonoParts],
            PaymentSettings.Unconfigured(new MonobankOptions(), new MonoPartsOptions()));

    [Fact]
    public void Unconfigured_empty_when_keys_set() =>
        Assert.Empty(PaymentSettings.Unconfigured(new MonobankOptions { Token = "t" }, new MonoPartsOptions { StoreId = "s", Secret = "x" }));

    [Fact]
    public void FromGospodar_normalises_and_refuses_a_broken_iban()
    {
        var rows = PaymentSettings.FromGospodar(new(" ТОВ «ЮА Грін» ", "12345678", "ua21 3223 1300 0002 6007 2335 6600 1", ""))!;
        Assert.Equal("ТОВ «ЮА Грін»", rows["pay_recipient"]);
        Assert.Equal("UA213223130000026007233566001", rows["pay_iban"]);
        Assert.Null(rows["pay_bank"]);
        Assert.Null(PaymentSettings.FromGospodar(new("ТОВ", null, "UA213223130000026007233566002", null)));
    }

    private sealed class Stub(Func<HttpResponseMessage?> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            respond() is { } r ? Task.FromResult(r) : throw new HttpRequestException("down");
    }

    private sealed class Cache : IOutputCacheStore
    {
        public int Evictions;
        public ValueTask EvictByTagAsync(string tag, CancellationToken ct) { Evictions++; return default; }
        public ValueTask<byte[]?> GetAsync(string key, CancellationToken ct) => default;
        public ValueTask SetAsync(string key, byte[] value, string[]? tags, TimeSpan validFor, CancellationToken ct) => default;
    }

    [Fact]
    public async Task Sync_mirrors_gospodar_and_keeps_the_copy_when_it_is_down()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase("pay-" + Guid.NewGuid()).Options);
        db.Settings.AddRange(new Setting { Key = "pay_recipient", Value = "Старий ФОП" }, new Setting { Key = "pay_bank", Value = "Старий банк" },
            new Setting { Key = "pay_purpose", Value = "Замовлення {n}" });
        await db.SaveChangesAsync();

        string? json = """{"recipient":"ТОВ «ЮА Грін»","edrpou":"12345678","iban":"UA213223130000026007233566001","bank":null}""";
        var http = new HttpClient(new Stub(() => json is null ? null : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) }))
            { BaseAddress = new Uri("https://erp.test") };
        var gospodar = new GospodarClient(http, Options.Create(new JwtOptions { SigningKey = new string('k', 32) }),
            Options.Create(new GospodarOptions { BaseUrl = "https://erp.test", IntegrationKey = "site-key" }), NullLogger<GospodarClient>.Instance);
        var cache = new Cache();
        async Task<Dictionary<string, string>> Rows() => await db.Settings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value);

        Assert.True(await PaymentSettings.SyncAsync(db, gospodar, cache, NullLogger.Instance, CancellationToken.None));
        var rows = await Rows();
        Assert.Equal("ТОВ «ЮА Грін»", rows["pay_recipient"]);
        Assert.Equal("UA213223130000026007233566001", rows["pay_iban"]);
        Assert.False(rows.ContainsKey("pay_bank"));             // emptied in Gospodar → gone here
        Assert.Equal("Замовлення {n}", rows["pay_purpose"]);    // the site's own, untouched
        Assert.Equal(1, cache.Evictions);

        Assert.True(await PaymentSettings.SyncAsync(db, gospodar, cache, NullLogger.Instance, CancellationToken.None));
        Assert.Equal(1, cache.Evictions);                        // nothing changed, public cache kept

        json = null;                                             // Gospodar down
        Assert.False(await PaymentSettings.SyncAsync(db, gospodar, cache, NullLogger.Instance, CancellationToken.None));
        Assert.Equal(rows, await Rows());
    }
}
