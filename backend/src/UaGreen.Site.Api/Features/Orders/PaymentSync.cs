using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Features.Audit;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Email;
using UaGreen.Site.Api.Infrastructure.Payments;
using UaGreen.Site.Api.Infrastructure.Persistence;

namespace UaGreen.Site.Api.Features.Orders;

public enum PayNotice { None, Paid, AmountMismatch, PartsApproved, PartsFailed, PartsCancelled }

/// The bank's webhook only wakes us: the status is always read from monobank with our own key, so a forged
/// call can at most trigger a re-read (no signature check needed) and out-of-order webhooks don't matter.
/// The same refresh runs when someone opens an order that still waits for the bank.
public sealed class PaymentSync(
    AppDbContext db, MonobankAcquiring card, MonoPartsClient parts, GospodarClient gospodar, Notifier notifier, ILogger<PaymentSync> log)
{
    public const string WaitingForStore = "IN_PROCESS/WAITING_FOR_STORE_CONFIRM";

    // ponytail: one global lock — the API runs as a single instance; use a row version if it ever scales out.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// Reads the bank state and applies it. Throws MonobankException / MonoPartsException when the bank fails.
    public async Task RefreshAsync(Order o, CancellationToken ct)
    {
        if (o.PaymentRef is null) return;
        PayNotice notice;
        await Gate.WaitAsync(ct);
        try
        {
            await db.Entry(o).ReloadAsync(ct); // a parallel webhook / page refresh may have applied it already
            var now = DateTime.UtcNow;
            notice = o.PaymentMethod switch
            {
                PaymentMethod.Card => ApplyCard(o, await card.GetStatusAsync(o.PaymentRef, ct), now),
                PaymentMethod.MonoParts => ApplyParts(o, await parts.StateAsync(o.PaymentRef, ct), now),
                _ => PayNotice.None,
            };
            if (notice != PayNotice.None) o.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            Gate.Release();
        }

        if (Subject(o, notice) is { } subject)
        {
            if (notice == PayNotice.AmountMismatch)
                log.LogWarning("monobank amount mismatch for order {Number}: {Status}", o.Number, o.PaymentStatus);
            await notifier.NotifyManagersAsync(subject,
                $"Замовлення №{o.Number}: {o.CustomerName}, {o.Phone}\nСума: {o.Total:N0} грн\nmonobank: {o.PaymentStatus} ({o.PaymentRef})", ct);
        }

        // Every refresh of a paid audit retries a failed Gospodar booking (Gospodar dedupes by ExternalId);
        // on its own token: the caller hanging up must not cancel the booking.
        if (AuditBooking.ShouldBook(o))
        {
            using var bookCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await AuditBooking.SendAsync(o, $"mono:{o.PaymentRef}", gospodar, notifier, bookCts.Token);
            await db.SaveChangesAsync(bookCts.Token);
        }
    }

    /// Refresh-on-read for pages: at most every 10 s, and a bank failure never breaks the page.
    public async Task RefreshIfStaleAsync(Order o, CancellationToken ct)
    {
        if (!Waiting(o) || o.PaymentCheckedAt > DateTime.UtcNow.AddSeconds(-10)) return;
        try
        {
            await RefreshAsync(o, ct);
        }
        catch (Exception ex) when (ex is MonobankException or MonoPartsException)
        {
            log.LogWarning("monobank refresh for order {Number} failed: {Error}", o.Number, ex.Message);
        }
    }

    /// Still waiting for the bank: unpaid with a bank reference, or pay-in-parts approved but not yet confirmed.
    public static bool Waiting(Order o) =>
        (o.Status == OrderStatus.AwaitingPayment && o.PaymentRef is not null)
        || (o.PaymentMethod == PaymentMethod.MonoParts && o.Status == OrderStatus.Paid && o.PaymentStatus?.StartsWith("IN_PROCESS/") == true);

    public static PayNotice ApplyCard(Order o, MonoInvoiceStatus s, DateTime now)
    {
        var was = o.PaymentStatus;
        o.PaymentStatus = s.Status;
        o.PaymentCheckedAt = now;
        if (s.Status != "success" || o.Status != OrderStatus.AwaitingPayment) return PayNotice.None;
        if (s.Amount != MonobankAcquiring.Kopecks(o.Total) || s.Ccy != 980)
            return was == "success" ? PayNotice.None : PayNotice.AmountMismatch; // a human looks once, not per webhook
        o.Status = OrderStatus.Paid;
        o.PaidAt = now;
        return PayNotice.Paid;
    }

    public static PayNotice ApplyParts(Order o, MonoPartsState s, DateTime now)
    {
        var was = o.PaymentStatus;
        o.PaymentStatus = s.Text;
        o.PaymentCheckedAt = now;
        if (o.Status == OrderStatus.AwaitingPayment)
        {
            if (s.Text == WaitingForStore || s.State == "SUCCESS")
            {
                o.Status = OrderStatus.Paid;
                o.PaidAt = now;
                return PayNotice.PartsApproved;
            }
            if (s.State == "FAIL")
            {
                o.Status = OrderStatus.New;
                return PayNotice.PartsFailed;
            }
        }
        // The bank dropped an application it had approved, before we confirmed the hand-over.
        if (o.Status == OrderStatus.Paid && was == WaitingForStore && s.State == "FAIL" && s.SubState != "REJECTED_BY_STORE")
        {
            o.Status = OrderStatus.New;
            o.PaidAt = null;
            return PayNotice.PartsCancelled;
        }
        return PayNotice.None;
    }

    private static string? Subject(Order o, PayNotice n) => n switch
    {
        PayNotice.Paid => $"Оплачено замовлення №{o.Number} — {o.Total:N0} грн",
        PayNotice.AmountMismatch => $"Сума оплати №{o.Number} не збігається — перевірте в кабінеті monobank",
        PayNotice.PartsApproved => $"Покупку частинами погоджено, замовлення №{o.Number} можна відвантажувати",
        PayNotice.PartsFailed => $"Банк не погодив покупку частинами №{o.Number}: {o.PaymentStatus}",
        PayNotice.PartsCancelled => $"Банк скасував покупку частинами №{o.Number} — не відвантажуйте без оплати",
        _ => null,
    };
}
