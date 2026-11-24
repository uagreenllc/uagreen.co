using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Features.Audit;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Email;
using UaGreen.Site.Api.Infrastructure.Payments;
using UaGreen.Site.Api.Infrastructure.Persistence;

namespace UaGreen.Site.Api.Features.Orders;

public enum PayNotice { None, Paid, AmountMismatch, PaidInactive, PartsApproved, PartsFailed, PartsCancelled }

/// The bank's webhook only wakes us: the status is always read from monobank with our own key, so a forged
/// call can at most trigger a re-read (no signature check needed) and out-of-order webhooks don't matter.
/// The same refresh runs when someone opens an order that still waits for the bank.
public sealed class PaymentSync(
    AppDbContext db, MonobankAcquiring card, MonoPartsClient parts, IServiceScopeFactory scopes, ILogger<PaymentSync> log)
{
    public const string WaitingForStore = "IN_PROCESS/WAITING_FOR_STORE_CONFIRM";

    /// What an order page may spend on the bank: the page itself gives the API 4 s.
    public static readonly TimeSpan PageBudget = TimeSpan.FromSeconds(2.5);

    // ponytail: one global lock and one in-process booking guard — the API runs as a single instance;
    // use a row version and a booking queue if it ever scales out.
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly ConcurrentDictionary<Guid, byte> Booking = new();

    /// Reads the bank state and applies it. `invoiceId` (card webhooks) names the invoice the bank called about,
    /// which may be an older one of this order. `ct` bounds only the wait and the bank read: once the bank has
    /// answered, the result is saved, and emails / the Gospodar booking run in the background.
    /// Throws MonobankException / MonoPartsException when the bank fails.
    public async Task RefreshAsync(Order o, CancellationToken ct, string? invoiceId = null)
    {
        var notice = PayNotice.None;
        await Gate.WaitAsync(ct);
        try
        {
            await db.Entry(o).ReloadAsync(ct); // a parallel webhook / page refresh may have applied it already
            var now = DateTime.UtcNow;
            if (o.PaymentMethod == PaymentMethod.Card && (invoiceId ?? o.PaymentRef) is { } inv)
            {
                var s = await card.GetStatusAsync(inv, ct);
                if (!AppliesTo(o, inv, s)) return;
                if (inv == o.PaymentRef) notice = ApplyCard(o, s, now);
                else if (o.Status == OrderStatus.AwaitingPayment)
                {
                    o.PaymentRef = inv; // an older invoice of this order got paid: it is the order's invoice now
                    notice = ApplyCard(o, s, now);
                }
                else notice = PayNotice.PaidInactive; // a second payment for an order that is already settled
            }
            else if (o.PaymentMethod == PaymentMethod.MonoParts && o.PaymentRef is not null)
                notice = ApplyParts(o, await parts.StateAsync(o.PaymentRef, ct), now);
            if (notice != PayNotice.None) o.UpdatedAt = now;
            await db.SaveChangesAsync(CancellationToken.None);
        }
        finally
        {
            Gate.Release();
        }

        if (notice == PayNotice.AmountMismatch)
            log.LogWarning("monobank amount mismatch for order {Number}: {Status}", o.Number, o.PaymentStatus);
        if (Subject(o, notice) is not null || AuditBooking.ShouldBook(o)) FollowUp(o, notice);
    }

    /// Manager email and the paid-audit Gospodar booking, after the response: neither may hold up the
    /// customer's page or the bank's webhook, and neither may die with their request.
    private void FollowUp(Order o, PayNotice notice)
    {
        var (id, subject) = (o.Id, Subject(o, notice));
        var text = $"Замовлення №{o.Number}: {o.CustomerName}, {o.Phone}\nСума: {o.Total:N0} грн\nmonobank: {o.PaymentStatus} ({o.PaymentRef})";
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopes.CreateScope();
                var sp = scope.ServiceProvider;
                var notifier = sp.GetRequiredService<Notifier>();
                if (subject is not null) await notifier.NotifyManagersAsync(subject, text, CancellationToken.None);

                // Every refresh of a paid audit retries a failed booking; only one runs at a time, on fresh data.
                if (!Booking.TryAdd(id, 0)) return;
                try
                {
                    var fresh = await sp.GetRequiredService<AppDbContext>().Orders.FirstAsync(x => x.Id == id);
                    if (!AuditBooking.ShouldBook(fresh)) return;
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                    await AuditBooking.SendAsync(fresh, $"mono:{fresh.PaymentRef}", sp.GetRequiredService<GospodarClient>(), notifier, cts.Token);
                    await sp.GetRequiredService<AppDbContext>().SaveChangesAsync(CancellationToken.None);
                }
                finally
                {
                    Booking.TryRemove(id, out _);
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, "monobank follow-up for order {Id} failed", id);
            }
        });
    }

    /// Refresh-on-read for pages: at most every 10 s, within PageBudget, and a bank failure never breaks the page.
    public async Task RefreshIfStaleAsync(Order o, CancellationToken ct)
    {
        if (!Waiting(o) || o.PaymentCheckedAt > DateTime.UtcNow.AddSeconds(-10)) return;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(PageBudget);
        try
        {
            await RefreshAsync(o, budget.Token);
        }
        catch (Exception ex) when (ex is MonobankException or MonoPartsException or OperationCanceledException)
        {
            log.LogWarning("monobank refresh for order {Number} skipped: {Error}", o.Number, ex.Message);
        }
    }

    /// Still waiting for the bank: unpaid with a bank reference, or pay-in-parts approved but not yet confirmed.
    public static bool Waiting(Order o) =>
        (o.Status == OrderStatus.AwaitingPayment && o.PaymentRef is not null)
        || (o.PaymentMethod == PaymentMethod.MonoParts && o.Status == OrderStatus.Paid && o.PaymentStatus?.StartsWith("IN_PROCESS/") == true);

    /// The order's current invoice always counts. Another invoice (an older one still open in a second tab, or
    /// retried after a failure) counts only when it was paid and the bank says it belongs to this order.
    public static bool AppliesTo(Order o, string invoiceId, MonoInvoiceStatus s) =>
        invoiceId == o.PaymentRef || (s.Status == "success" && s.Reference == o.Id.ToString());

    public static PayNotice ApplyCard(Order o, MonoInvoiceStatus s, DateTime now)
    {
        var was = o.PaymentStatus;
        o.PaymentStatus = s.Status;
        o.PaymentCheckedAt = now;
        if (s.Status != "success") return PayNotice.None;
        if (o.Status != OrderStatus.AwaitingPayment)
            // Paid after the manager closed the order (the invoice page was still open): never reopen it, tell a human once.
            return was == "success" || o.Status is OrderStatus.Paid or OrderStatus.Shipped ? PayNotice.None : PayNotice.PaidInactive;
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
        if (s.SubState == "REJECTED_BY_STORE") return PayNotice.None; // our own reject coming back as a callback
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
        if (o.Status == OrderStatus.Paid && was == WaitingForStore && s.State == "FAIL")
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
        PayNotice.PaidInactive => $"Оплачено неактивне замовлення №{o.Number} ({o.Status}) — перевірте в кабінеті monobank",
        PayNotice.PartsApproved => $"Покупку частинами погоджено, замовлення №{o.Number} можна відвантажувати",
        PayNotice.PartsFailed => $"Банк не погодив покупку частинами №{o.Number}: {o.PaymentStatus}",
        PayNotice.PartsCancelled => $"Банк скасував покупку частинами №{o.Number} — не відвантажуйте без оплати",
        _ => null,
    };
}
