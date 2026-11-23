using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Features.Orders;

namespace UaGreen.Site.Tests;

public class PaymentSettingsTests
{
    [Theory]
    [InlineData("UA213223130000026007233566001", true)]
    [InlineData("GB82WEST12345698765432", true)]
    [InlineData("UA213223130000026007233566002", false)] // one digit off
    [InlineData("UA21322313000002600723356600", false)]  // UA must be 29 chars
    [InlineData("ua213223130000026007233566001", false)] // normalised by Validate, not here
    public void IsValidIban(string iban, bool ok) => Assert.Equal(ok, PaymentSettings.IsValidIban(iban));

    [Fact]
    public void Validate_normalises_iban_and_fills_missing_keys()
    {
        var req = new Dictionary<string, string?> { ["pay_iban"] = " ua21 3223 1300 0002 6007 2335 6600 1 ", ["pay_Credit"] = "off" };
        PaymentSettings.Validate(req);
        Assert.Equal("UA213223130000026007233566001", req["pay_iban"]);
        Assert.Null(req["pay_LiqPay"]); // left out → cleared → on
        Assert.Equal(PaymentSettings.Keys.Length, req.Count);
    }

    [Fact]
    public void Validate_rejects_bad_input()
    {
        void Bad(Dictionary<string, string?> req) => Assert.Throws<ProblemException>(() => PaymentSettings.Validate(req));
        Bad(new() { ["pay_iban"] = "UA213223130000026007233566002" });
        Bad(new() { ["pay_edrpou"] = "1234567" });
        Bad(new() { ["pay_LiqPay"] = "on" });
        Bad(new() { ["code_head"] = "<script>" });
        Bad(new() { ["pay_LiqPay"] = "off", ["pay_Invoice"] = "off", ["pay_Credit"] = "off" });
    }
}
