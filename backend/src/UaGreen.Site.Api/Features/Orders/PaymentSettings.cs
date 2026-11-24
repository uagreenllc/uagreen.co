using System.Text.RegularExpressions;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Payments;

namespace UaGreen.Site.Api.Features.Orders;

/// "Платіжні системи" page: Setting rows with the pay_ prefix. Public (the site shows the
/// requisites after an Invoice order), but Admin-only to write: the IBAN is where money goes.
public static partial class PaymentSettings
{
    public const string Prefix = "pay_";

    /// pay_Card = "off" hides the method; no row = on, as before the page existed.
    public static string OffKey(PaymentMethod m) => Prefix + m;
    public const string Off = "off";

    /// Max number of monobank pay-in-parts payments offered (3..25, empty = 6).
    public const string PartsMax = "pay_parts_max";

    /// Company requisites for the manual method (bank transfer to IBAN).
    public static readonly string[] Requisites = ["pay_recipient", "pay_edrpou", "pay_iban", "pay_bank", "pay_purpose"];
    public static readonly string[] Keys = [.. Enum.GetValues<PaymentMethod>().Select(OffKey), .. Requisites, PartsMax];

    /// Methods whose bank keys are missing on the server: hidden on the site and refused by the API, whatever the switch says.
    public static IReadOnlyList<PaymentMethod> Unconfigured(MonobankOptions card, MonoPartsOptions parts) =>
        [.. card.Configured ? [] : new[] { PaymentMethod.Card }, .. parts.Configured ? [] : new[] { PaymentMethod.MonoParts }];

    /// The admin page sends only the keys that changed (null = delete); `current` is what is stored now.
    /// Normalises values in place.
    public static void Validate(Dictionary<string, string?> req, IReadOnlyDictionary<string, string> current)
    {
        var unknown = req.Keys.Except(Keys).ToList();
        if (unknown.Count > 0) throw ProblemException.BadRequest($"Unknown keys: {string.Join(", ", unknown)}");

        string? After(string k) => req.TryGetValue(k, out var v) ? v : current.GetValueOrDefault(k);
        var methods = Enum.GetValues<PaymentMethod>();
        if (methods.Any(m => req.GetValueOrDefault(OffKey(m)) is { Length: > 0 } v && v != Off))
            throw ProblemException.BadRequest("A method is either \"off\" or empty");
        if (methods.All(m => After(OffKey(m)) == Off))
            throw ProblemException.BadRequest("Залиште увімкненим хоча б один спосіб оплати");

        if (req.ContainsKey("pay_iban"))
        {
            var iban = Regex.Replace(req["pay_iban"] ?? "", @"\s", "").ToUpperInvariant();
            if (iban.Length > 0 && !IsValidIban(iban)) throw ProblemException.BadRequest("IBAN з помилкою — перевірте цифри");
            req["pay_iban"] = iban;
        }

        var code = req.GetValueOrDefault("pay_edrpou")?.Trim() ?? "";
        if (code.Length > 0 && !EdrpouRx().IsMatch(code))
            throw ProblemException.BadRequest("ЄДРПОУ — 8 цифр (або 10 цифр РНОКПП для ФОП)");

        if (req.GetValueOrDefault(PartsMax)?.Trim() is { Length: > 0 } max)
        {
            if (!int.TryParse(max, out var n) || n is < 3 or > 25) throw ProblemException.BadRequest("Максимум платежів — від 3 до 25");
            req[PartsMax] = n.ToString();
        }
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
