using System.Text.RegularExpressions;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Features.Admin;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Payments;
using UaGreen.Site.Api.Infrastructure.Persistence;

namespace UaGreen.Site.Api.Features.Orders;

/// "Платіжні системи" page: Setting rows with the pay_ prefix. Public (the site shows the
/// requisites after an Invoice order), but Admin-only to write: the IBAN is where money goes.
public static partial class PaymentSettings
{
    public const string Prefix = "pay_";

    /// pay_Card = "off" hides the method; no row = on, as before the page existed.
    public static string OffKey(PaymentMethod m) => Prefix + m;
    public const string Off = "off";

    /// Company requisites for the manual method (bank transfer to IBAN): a copy of Gospodar's
    /// Компанії → реквізити (SyncAsync), so the IBAN is edited in one place only.
    public static readonly string[] Requisites = ["pay_recipient", "pay_edrpou", "pay_iban", "pay_bank"];

    /// Max number of monobank pay-in-parts payments offered (3..25, empty = 12).
    public const string PartsMax = "pay_parts_max";

    /// What the admin page writes: methods on/off, the payment purpose and the pay-in-parts maximum.
    public static readonly string[] Keys = [.. Enum.GetValues<PaymentMethod>().Select(OffKey), "pay_purpose", PartsMax];

    /// Methods whose bank keys are missing on the server: hidden on the site and refused by the API, whatever the switch says.
    public static IReadOnlyList<PaymentMethod> Unconfigured(MonobankOptions card, MonoPartsOptions parts) =>
        [.. card.Configured ? [] : new[] { PaymentMethod.Card }, .. parts.Configured ? [] : new[] { PaymentMethod.MonoParts }];

    /// The admin page sends only the changed keys; `stored` holds the rest for the "one method stays on" check.
    public static void Validate(Dictionary<string, string?> req, IReadOnlyDictionary<string, string> stored)
    {
        if (req.Keys.Intersect(Requisites).Any()) throw ProblemException.BadRequest("Реквізити змінюються в Господарі: Компанії → реквізити");
        var unknown = req.Keys.Except(Keys).ToList();
        if (unknown.Count > 0) throw ProblemException.BadRequest($"Unknown keys: {string.Join(", ", unknown)}");

        var methods = Enum.GetValues<PaymentMethod>();
        if (methods.Any(m => req.GetValueOrDefault(OffKey(m)) is { Length: > 0 } v && v != Off))
            throw ProblemException.BadRequest("A method is either \"off\" or empty");
        string? Now(string k) => req.TryGetValue(k, out var v) ? v : stored.GetValueOrDefault(k);
        if (methods.All(m => Now(OffKey(m)) == Off))
            throw ProblemException.BadRequest("Залиште увімкненим хоча б один спосіб оплати");

        if (req.GetValueOrDefault(PartsMax)?.Trim() is { Length: > 0 } max)
        {
            if (!int.TryParse(max, out var n) || n is < 3 or > 25) throw ProblemException.BadRequest("Максимум платежів — від 3 до 25");
            req[PartsMax] = n.ToString();
        }
    }

    /// Gospodar's requisites as pay_ rows (null = no row). Null when the IBAN fails its checksum:
    /// the site then keeps its last good copy instead of showing customers a broken account.
    public static Dictionary<string, string?>? FromGospodar(GospodarRequisites r)
    {
        var iban = Regex.Replace(r.Iban ?? "", @"\s", "").ToUpperInvariant();
        if (iban.Length > 0 && !IsValidIban(iban)) return null;
        return new()
        {
            ["pay_recipient"] = AdminCommon.Clean(r.Recipient),
            ["pay_edrpou"] = AdminCommon.Clean(r.Edrpou),
            ["pay_iban"] = AdminCommon.Clean(iban),
            ["pay_bank"] = AdminCommon.Clean(r.Bank),
        };
    }

    /// Copies Gospodar's requisites into the pay_ rows. False when Gospodar didn't answer (no key, down)
    /// or sent a broken IBAN — what the site has stays.
    public static async Task<bool> SyncAsync(AppDbContext db, GospodarClient gospodar, IOutputCacheStore cache, ILogger log, CancellationToken ct)
    {
        if (await gospodar.GetRequisitesAsync(ct) is not { } r) return false;
        if (FromGospodar(r) is not { } fresh)
        {
            log.LogWarning("Gospodar requisites: IBAN {Iban} fails the checksum, keeping the site's copy", r.Iban);
            return false;
        }
        var rows = await db.Settings.Where(s => Requisites.Contains(s.Key)).ToDictionaryAsync(s => s.Key, ct);
        var changed = false;
        foreach (var (key, value) in fresh)
        {
            var row = rows.GetValueOrDefault(key);
            if (row?.Value == value) continue;
            changed = true;
            if (value is null) db.Settings.Remove(row!);
            else if (row is null) db.Settings.Add(new Setting { Key = key, Value = value });
            else row.Value = value;
        }
        if (!changed) return true;
        await db.SaveChangesAsync(ct);
        await AdminCommon.EvictPublicAsync(cache, ct);
        log.LogInformation("Payment requisites updated from Gospodar: {Recipient}, IBAN {Iban}", fresh["pay_recipient"], fresh["pay_iban"]);
        return true;
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
}

/// Keeps the requisites fresh without anyone opening the admin page.
public sealed class PaymentRequisitesSync(IServiceScopeFactory scopes, ILogger<PaymentRequisitesSync> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var sp = scope.ServiceProvider;
                await PaymentSettings.SyncAsync(sp.GetRequiredService<AppDbContext>(), sp.GetRequiredService<GospodarClient>(),
                    sp.GetRequiredService<IOutputCacheStore>(), log, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogError(ex, "Payment requisites sync failed"); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
