using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Features.Orders;
using UaGreen.Site.Api.Infrastructure.Payments;

namespace UaGreen.Site.Tests;

public class PaymentSettingsTests
{
    private static readonly Dictionary<string, string> None = [];

    [Theory]
    [InlineData("UA213223130000026007233566001", true)]
    [InlineData("GB82WEST12345698765432", true)]
    [InlineData("UA213223130000026007233566002", false)] // one digit off
    [InlineData("UA21322313000002600723356600", false)]  // UA must be 29 chars
    [InlineData("ua213223130000026007233566001", false)] // normalised by Validate, not here
    public void IsValidIban(string iban, bool ok) => Assert.Equal(ok, PaymentSettings.IsValidIban(iban));

    [Fact]
    public void Validate_normalises_iban()
    {
        var req = new Dictionary<string, string?> { ["pay_iban"] = " ua21 3223 1300 0002 6007 2335 6600 1 ", ["pay_Credit"] = "off" };
        PaymentSettings.Validate(req, None);
        Assert.Equal("UA213223130000026007233566001", req["pay_iban"]);
    }

    // The admin page sends only what changed: everything else must stay as it is.
    [Fact]
    public void Left_out_keys_are_not_touched()
    {
        var req = new Dictionary<string, string?> { ["pay_Credit"] = "off" };
        PaymentSettings.Validate(req, new Dictionary<string, string> { ["pay_iban"] = "UA213223130000026007233566001" });
        Assert.Equal(["pay_Credit"], req.Keys);
    }

    private static void Bad(Dictionary<string, string?> req, Dictionary<string, string>? current = null) =>
        Assert.Throws<ProblemException>(() => PaymentSettings.Validate(req, current ?? None));

    [Fact]
    public void Validate_rejects_bad_input()
    {
        Bad(new() { ["pay_iban"] = "UA213223130000026007233566002" });
        Bad(new() { ["pay_edrpou"] = "1234567" });
        Bad(new() { ["pay_Card"] = "on" });
        Bad(new() { ["code_head"] = "<script>" });
        Bad(new() { ["pay_Card"] = "off", ["pay_Invoice"] = "off", ["pay_Credit"] = "off", ["pay_MonoParts"] = "off" });
    }

    [Fact]
    public void All_off_counts_current_values() =>
        Bad(new() { ["pay_MonoParts"] = "off" }, new() { ["pay_Card"] = "off", ["pay_Invoice"] = "off", ["pay_Credit"] = "off" });

    [Fact]
    public void Turning_one_back_on_is_fine()
    {
        var req = new Dictionary<string, string?> { ["pay_Card"] = null };
        PaymentSettings.Validate(req, new Dictionary<string, string> { ["pay_Card"] = "off", ["pay_Invoice"] = "off", ["pay_Credit"] = "off", ["pay_MonoParts"] = "off" });
    }

    [Theory]
    [InlineData("2")]
    [InlineData("26")]
    [InlineData("abc")]
    public void Parts_max_out_of_range(string v) => Bad(new() { ["pay_parts_max"] = v });

    [Fact]
    public void Parts_max_ok()
    {
        var req = new Dictionary<string, string?> { ["pay_parts_max"] = " 10 " };
        PaymentSettings.Validate(req, None);
        Assert.Equal("10", req["pay_parts_max"]);
    }

    [Fact]
    public void Unconfigured_lists_methods_without_keys() =>
        Assert.Equal([PaymentMethod.Card, PaymentMethod.MonoParts],
            PaymentSettings.Unconfigured(new MonobankOptions(), new MonoPartsOptions()));

    [Fact]
    public void Unconfigured_empty_when_keys_set() =>
        Assert.Empty(PaymentSettings.Unconfigured(new MonobankOptions { Token = "t" }, new MonoPartsOptions { StoreId = "s", Secret = "x" }));
}
