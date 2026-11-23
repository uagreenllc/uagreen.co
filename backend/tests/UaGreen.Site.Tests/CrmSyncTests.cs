using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Crm;

namespace UaGreen.Site.Tests;

public class CrmSyncTests
{
    [Fact]
    public void Retry_backoff_doubles_and_stops()
    {
        var now = new DateTime(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);
        var c = new ContactRequest();
        Assert.True(CrmSync.Due(c, now));                                    // never tried

        c.CrmAttempts = 1; c.CrmLastAttemptAt = now.AddSeconds(-30);
        Assert.False(CrmSync.Due(c, now));                                   // 1 min after the 1st try
        c.CrmLastAttemptAt = now.AddMinutes(-1);
        Assert.True(CrmSync.Due(c, now));

        c.CrmAttempts = 4; c.CrmLastAttemptAt = now.AddMinutes(-7);
        Assert.False(CrmSync.Due(c, now));                                   // 8 min after the 4th
        c.CrmAttempts = 15; c.CrmLastAttemptAt = now.AddHours(-6);
        Assert.True(CrmSync.Due(c, now));                                    // capped at 6 h

        c.CrmAttempts = CrmSync.MaxAttempts; c.CrmLastAttemptAt = now.AddDays(-1);
        Assert.False(CrmSync.Due(c, now));                                   // gave up
        c.CrmAttempts = 1; c.CrmDealId = Guid.NewGuid();
        Assert.False(CrmSync.Due(c, now));                                   // already a deal
    }

    [Fact]
    public void Order_lead_fits_gospodar_limits()
    {
        var o = new Order
        {
            Number = 42, CustomerName = "Іван", Phone = "+380501112233", PaymentMethod = PaymentMethod.Invoice,
            DeliveryMethod = DeliveryMethod.NovaPoshta, DeliveryCity = "Львів", Total = 1000,
            Items = Enumerable.Range(1, 40).Select(i => new OrderItem { ProductName = $"Вітрогенератор K5 варіант {i}", Qty = 1, UnitPrice = 25, LineTotal = 25 }).ToList(),
        };
        var lead = CrmSync.LeadFor(o);
        Assert.Equal("site-order-42", lead.ExternalId);
        Assert.Equal(CrmSync.Source, lead.Source);
        Assert.StartsWith("Замовлення №42", lead.Comment);
        Assert.True(lead.Comment!.Length <= 1000);
        Assert.True(lead.ProductInterest!.Length <= 200);

        var c = new ContactRequest { Id = Guid.NewGuid(), Name = "Оля", Phone = "0501112233", Message = "Скільки коштує K5?" };
        Assert.True(CrmSync.LeadFor(c).ExternalId.Length <= 60);
    }

    [Fact]
    public void Since_parses_the_stored_switch_time()
    {
        Assert.Null(CrmSync.Since(null));
        Assert.Null(CrmSync.Since("on"));
        Assert.Equal(new DateTime(2026, 11, 23, 4, 0, 0, DateTimeKind.Utc), CrmSync.Since("2026-11-23T04:00:00.0000000Z"));
    }
}
