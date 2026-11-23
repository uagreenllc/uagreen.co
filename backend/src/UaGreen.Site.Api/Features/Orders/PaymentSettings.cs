using System.Text.RegularExpressions;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Domain;

namespace UaGreen.Site.Api.Features.Orders;

/// "Платіжні системи" page: Setting rows with the pay_ prefix. Public (the site shows the
/// requisites after an Invoice order), but Admin-only to write: the IBAN is where money goes.
public static partial class PaymentSettings
{
    public const string Prefix = "pay_";

    /// pay_LiqPay = "off" hides the method; no row = on, as before the page existed.
    public static string OffKey(PaymentMethod m) => Prefix + m;
    public const string Off = "off";

    /// Company requisites for the manual method (bank transfer to IBAN).
    public static readonly string[] Requisites = ["pay_recipient", "pay_edrpou", "pay_iban", "pay_bank", "pay_purpose"];
    public static readonly string[] Keys = [.. Enum.GetValues<PaymentMethod>().Select(OffKey), .. Requisites];

    /// The admin page sends the whole set; keys it leaves out are cleared. Normalises the IBAN in place.
    public static void Validate(Dictionary<string, string?> req)
    {
        var unknown = req.Keys.Except(Keys).ToList();
        if (unknown.Count > 0) throw ProblemException.BadRequest($"Unknown keys: {string.Join(", ", unknown)}");
        foreach (var k in Keys) req.TryAdd(k, null);

        var methods = Enum.GetValues<PaymentMethod>();
        if (methods.Any(m => req[OffKey(m)] is { Length: > 0 } v && v != Off))
            throw ProblemException.BadRequest("A method is either \"off\" or empty");
        if (methods.All(m => req[OffKey(m)] == Off))
            throw ProblemException.BadRequest("Залиште увімкненим хоча б один спосіб оплати");

        var iban = Regex.Replace(req["pay_iban"] ?? "", @"\s", "").ToUpperInvariant();
        if (iban.Length > 0 && !IsValidIban(iban)) throw ProblemException.BadRequest("IBAN з помилкою — перевірте цифри");
        req["pay_iban"] = iban;

        var code = req["pay_edrpou"]?.Trim() ?? "";
        if (code.Length > 0 && !EdrpouRx().IsMatch(code))
            throw ProblemException.BadRequest("ЄДРПОУ — 8 цифр (або 10 цифр РНОКПП для ФОП)");
    }

    /// ISO 13616 mod-97 check; Ukrainian IBANs are also exactly 29 characters.
    public static bool IsValidIban(string iban)
    {
        if (!IbanRx().IsMatch(iban) || (iban.StartsWith("UA") && iban.Length != 29)) return false;
        var rem = 0;
        foreach (var c in iban[4..] + iban[..4]) // letters count as 10..35, i.e. two digits
            rem = char.IsDigit(c) ? (rem * 10 + (c - '0')) % 97 : (rem * 100 + (c - 'A' + 10)) % 97;
        return rem == 1;
    }

    [GeneratedRegex("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$")]
    private static partial Regex IbanRx();

    [GeneratedRegex(@"^\d{8}(\d{2})?$")]
    private static partial Regex EdrpouRx();
}
