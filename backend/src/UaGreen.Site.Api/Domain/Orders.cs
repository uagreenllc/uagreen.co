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

public enum PaymentMethod { LiqPay, Invoice, Credit }

public enum DeliveryMethod { NovaPoshta, Ukrposhta, Pickup }

public sealed class Order
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

    public string? LiqPayStatus { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<OrderItem> Items { get; set; } = [];
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
