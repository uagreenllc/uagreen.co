using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Features.Orders;

namespace UaGreen.Site.Tests;

public class OrderPricingTests
{
    private static readonly Guid Mast6 = Guid.NewGuid(), Mast9 = Guid.NewGuid(), ZoneA = Guid.NewGuid();

    private static Product Turbine()
    {
        var p = new Product { Id = Guid.NewGuid(), NameUk = "ВЕС 1кВт", NameEn = "WPP 1kW", Price = 47000m, Published = true, InStock = true };
        var mast = new ProductOption { Id = Guid.NewGuid(), NameUk = "Щогла", Values =
        {
            new ProductOptionValue { Id = Mast6, ValueUk = "6 м", PriceDelta = 0 },
            new ProductOptionValue { Id = Mast9, ValueUk = "9 м", PriceDelta = 5000m },
        }};
        var zone = new ProductOption { Id = Guid.NewGuid(), NameUk = "Зона", Values = { new ProductOptionValue { Id = ZoneA, ValueUk = "Захід", PriceDelta = -500m } } };
        p.Options.AddRange([mast, zone]);
        return p;
    }

    private static Product Blade() => new() { Id = Guid.NewGuid(), NameUk = "Лопать", Price = 6000m, Published = true, InStock = true };

    [Fact]
    public void Sums_base_price_plus_option_deltas_times_qty()
    {
        var t = Turbine(); var b = Blade();
        var products = new Dictionary<Guid, Product> { [t.Id] = t, [b.Id] = b };
        var (subtotal, items) = OrderPricing.Compute(
        [
            new OrderLineRequest(t.Id, 2, [Mast9, ZoneA]),
            new OrderLineRequest(b.Id, 3, null),
        ], products, "en");

        Assert.Equal(2 * (47000m + 5000m - 500m) + 3 * 6000m, subtotal);
        Assert.Equal(2, items.Count);
        Assert.Equal("WPP 1kW", items[0].ProductName);          // EN name picked
        Assert.Equal("Щогла: 9 м; Зона: Захід", items[0].OptionsText); // EN missing → UK fallback
        Assert.Equal(51500m, items[0].UnitPrice);
        Assert.Equal(18000m, items[1].LineTotal);
    }

    [Fact]
    public void Rejects_missing_option_foreign_value_bad_qty_and_out_of_stock()
    {
        var t = Turbine();
        var products = new Dictionary<Guid, Product> { [t.Id] = t };

        Assert.Equal(400, Assert.Throws<ProblemException>(() => OrderPricing.Compute([new(t.Id, 1, [Mast6])], products, "uk")).StatusCode);        // zone not chosen
        Assert.Equal(400, Assert.Throws<ProblemException>(() => OrderPricing.Compute([new(t.Id, 1, [Mast6, ZoneA, Guid.NewGuid()])], products, "uk")).StatusCode);
        Assert.Equal(400, Assert.Throws<ProblemException>(() => OrderPricing.Compute([new(t.Id, 0, [Mast6, ZoneA])], products, "uk")).StatusCode);
        Assert.Equal(400, Assert.Throws<ProblemException>(() => OrderPricing.Compute([new(Guid.NewGuid(), 1, null)], products, "uk")).StatusCode);
        Assert.Equal(400, Assert.Throws<ProblemException>(() => OrderPricing.Compute([], products, "uk")).StatusCode);

        t.InStock = false;
        Assert.Equal(400, Assert.Throws<ProblemException>(() => OrderPricing.Compute([new(t.Id, 1, [Mast6, ZoneA])], products, "uk")).StatusCode);
    }
}
