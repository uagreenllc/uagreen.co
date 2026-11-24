using UaGreen.Site.Api.Domain;

namespace UaGreen.Site.Api.Features.Admin;

public enum BankStep { None, Confirm, Reject, Block }

/// What a manager's status change means for a monobank pay-in-parts application.
public static class AdminPaymentRules
{
    /// `o.PaymentStatus` must be fresh (PaymentSync.RefreshAsync) before asking.
    public static (BankStep Step, string? Message) ForStatus(Order o, OrderStatus to)
    {
        if (o.PaymentMethod != PaymentMethod.MonoParts || o.PaymentRef is null) return (BankStep.None, null);
        var open = o.PaymentStatus?.StartsWith("IN_PROCESS/") == true;
        return to switch
        {
            // Goods leave → the bank activates the plan and pays the store.
            OrderStatus.Shipped when o.PaymentStatus == "IN_PROCESS/WAITING_FOR_STORE_CONFIRM" => (BankStep.Confirm, null),
            OrderStatus.Shipped when open => (BankStep.Block, "Клієнт ще не підтвердив покупку частинами"),
            OrderStatus.Cancelled when open => (BankStep.Reject, null),
            _ => (BankStep.None, null),
        };
    }

    /// The bank already holds this amount (an open invoice or application): changing Total would desync them.
    public static bool TotalLocked(Order o) =>
        o.PaymentMethod is PaymentMethod.Card or PaymentMethod.MonoParts
        && o.Status == OrderStatus.AwaitingPayment && o.PaymentRef is not null;
}
