namespace UaGreen.Site.Api.Domain;

public enum OrderStatus
{
    New,
    AwaitingPayment,
    Paid,
    InvoiceSent,
    Credit,
    Shipped,
    Cancelled,
}

/// Card = monobank internet acquiring; Credit = the manual state 0 % energy credit; MonoParts = monobank «Покупка частинами».
public enum PaymentMethod { Card, Invoice, Credit, MonoParts }

public enum DeliveryMethod { NovaPoshta, Ukrposhta, Pickup }

/// Rows that become Gospodar CRM deals (orders, contact requests).
public interface ICrmLinked
{
    Guid? CrmDealId { get; set; }
    DateTime? CrmSyncedAt { get; set; }
    string? CrmError { get; set; }
    int CrmAttempts { get; set; }
    DateTime? CrmLastAttemptAt { get; set; }
}

public sealed class Order : ICrmLinked
{
    /// Public token: result_url / status page use it (sequential Number would leak volume).
    public Guid Id { get; set; }
    /// Human order number, assigned by the DB sequence.
    public int Number { get; set; }
    public OrderStatus Status { get; set; }

    public string CustomerName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public DeliveryMethod DeliveryMethod { get; set; }
    public string? DeliveryCity { get; set; }
    public string? DeliveryWarehouse { get; set; }
    public string? Comment { get; set; }
    public PaymentMethod PaymentMethod { get; set; }

    public decimal Subtotal { get; set; }
    // ponytail: delivery is paid to the carrier on receipt; add Nova Poshta API when asked.
    public decimal DeliveryCost { get; set; }
    public decimal Total { get; set; }

    // Site audit (the "Аудит вітру на ділянці" product bought from /audit/book); null on shop orders.
    public DateOnly? AuditDate { get; set; }
    public string? AuditAddress { get; set; }
    public DateTime? OfferAcceptedAt { get; set; }
    /// When Gospodar accepted the booking; null = not (yet) booked there.
    public DateTime? GospodarBookedAt { get; set; }
    /// Gospodar's answer: false = the day was taken meanwhile and a manager will reschedule.
    public bool? AuditDateWasFree { get; set; }

    /// Bank-side state: acquiring invoice status (`success`, …) or pay-in-parts `STATE/SUB_STATE`.
    public string? PaymentStatus { get; set; }
    /// monobank invoiceId (Card) or pay-in-parts order_id (MonoParts); webhooks find the order by it.
    public string? PaymentRef { get; set; }
    /// Hosted payment page of the current Card invoice.
    public string? PayUrl { get; set; }
    /// Last time the status was read from the bank (throttles refresh-on-read).
    public DateTime? PaymentCheckedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// Manager-only note, never shown to the customer.
    public string? AdminNote { get; set; }

    public Guid? CrmDealId { get; set; }
    public DateTime? CrmSyncedAt { get; set; }
    public string? CrmError { get; set; }
    public int CrmAttempts { get; set; }
    public DateTime? CrmLastAttemptAt { get; set; }

    public List<OrderItem> Items { get; set; } = [];
    public List<OrderEvent> Events { get; set; } = [];
}

/// Who changed what on an order (status, items, contacts) — shown as the order history.
public sealed class OrderEvent
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public DateTime At { get; set; }
    public string Actor { get; set; } = "";
    public string Text { get; set; } = "";
}

public sealed class OrderItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid? ProductId { get; set; }
    // Snapshots: prices and names change, orders must not.
    public string ProductName { get; set; } = "";
    public string? OptionsText { get; set; }
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
