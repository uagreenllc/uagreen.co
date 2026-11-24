using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Features.Audit;

namespace UaGreen.Site.Tests;

public class AuditBookingTests
{
    private static Order Audit(OrderStatus status) => new()
    {
        Id = Guid.NewGuid(), Number = 7, Status = status, CustomerName = "Олена", Phone = "+380501112233",
        Total = 15000m, AuditDate = new DateOnly(2026, 12, 8), AuditAddress = "с. Хроли, вул. Садова 1",
        CreatedAt = new DateTime(2026, 11, 23, 9, 0, 0, DateTimeKind.Utc),
        OfferAcceptedAt = new DateTime(2026, 11, 23, 9, 58, 0, DateTimeKind.Utc),
        PaidAt = status == OrderStatus.Paid ? new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc) : null,
    };

    [Fact]
    public void ShouldBook_false_unless_paid()
    {
        Assert.False(AuditBooking.ShouldBook(Audit(OrderStatus.AwaitingPayment)));
        Assert.True(AuditBooking.ShouldBook(Audit(OrderStatus.Paid)));
        var shop = Audit(OrderStatus.Paid);
        shop.AuditDate = null;
        Assert.False(AuditBooking.ShouldBook(shop));
    }

    [Fact]
    public void ShouldBook_false_once_booked()
    {
        var o = Audit(OrderStatus.Paid);
        o.GospodarBookedAt = DateTime.UtcNow;
        Assert.False(AuditBooking.ShouldBook(o));
    }

    [Fact]
    public void ToRequest_maps_payment_and_dates()
    {
        var o = Audit(OrderStatus.Paid);

        var r = AuditBooking.ToRequest(o, "mono:inv_1");

        Assert.Equal(o.Id.ToString(), r.ExternalId);
        Assert.Equal(15000m, r.Amount);
        Assert.Equal("mono:inv_1", r.PaymentRef);
        Assert.Equal(new DateOnly(2026, 12, 8), r.Date);
        Assert.Equal(o.PaidAt, r.PaidAt);
        Assert.Equal(o.OfferAcceptedAt, r.OfferAcceptedAt);
        Assert.Equal("с. Хроли, вул. Садова 1", r.Address);
    }
}
