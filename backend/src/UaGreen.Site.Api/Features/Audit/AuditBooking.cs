using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Email;

namespace UaGreen.Site.Api.Features.Audit;

/// After monobank confirms an audit order: book it in Gospodar (customer, deal, order, trip, payment there).
public static class AuditBooking
{
    /// Paid audit not yet accepted by Gospodar. Because it checks GospodarBookedAt, a later payment sync
    /// for the order retries a failed booking; Gospodar dedupes by ExternalId.
    /// ponytail: no retry queue — the manager email is the retry; add a background retry if failures show up.
    public static bool ShouldBook(Order o) =>
        o.Status == OrderStatus.Paid && o.AuditDate is not null && o.GospodarBookedAt is null;

    public static GospodarAuditBooking ToRequest(Order o, string paymentRef) => new(
        o.Id.ToString(), o.CustomerName, o.Phone, o.Email, o.AuditAddress ?? "", o.Comment,
        o.AuditDate!.Value, o.Total, o.PaidAt ?? DateTime.UtcNow, paymentRef, o.OfferAcceptedAt ?? o.CreatedAt);

    /// Sets the booking result on the order (the caller saves) and tells managers when a human has to step in.
    public static async Task SendAsync(Order o, string paymentRef, GospodarClient gospodar, Notifier notifier, CancellationToken ct)
    {
        var req = ToRequest(o, paymentRef);
        var booked = await gospodar.BookAuditAsync(req, ct);
        if (booked is null)
        {
            await notifier.NotifyManagersAsync($"Аудит №{o.Number} оплачено, але НЕ передано в Господар",
                "Спершу перевірте в Господарі (після таймауту бронювання могло створитися), інакше забронюйте вручну:\n" +
                $"Клієнт: {req.Name}, {req.Phone}{(req.Email is null ? "" : ", " + req.Email)}\n" +
                $"Адреса: {req.Address}\nДата: {req.Date:dd.MM.yyyy}\n" +
                $"{(req.Comment is null ? "" : $"Коментар: {req.Comment}\n")}" +
                $"Сплачено: {req.Amount:N0} грн, {req.PaymentRef}, {req.PaidAt:dd.MM.yyyy HH:mm} UTC\n" +
                $"Замовлення сайту: {req.ExternalId}", ct);
            return;
        }

        o.GospodarBookedAt = DateTime.UtcNow;
        o.AuditDateWasFree = booked.DateWasFree;
        if (!booked.DateWasFree)
            await notifier.NotifyManagersAsync($"Аудит №{o.Number}: дата {req.Date:dd.MM.yyyy} уже зайнята",
                $"Господар створив бронювання й задачу на перенесення. Клієнт: {req.Name}, {req.Phone}.", ct);
    }
}
