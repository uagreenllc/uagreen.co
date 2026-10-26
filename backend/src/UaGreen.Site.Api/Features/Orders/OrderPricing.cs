using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Domain;

namespace UaGreen.Site.Api.Features.Orders;

public sealed record OrderLineRequest(Guid ProductId, int Qty, List<Guid>? OptionValueIds);

/// Server-side pricing: the client sends product ids, quantities and chosen option
/// values; prices come from the DB. Pure so it is unit-testable.
public static class OrderPricing
{
    public const int MaxLines = 50;
    public const int MaxQty = 100;

    public static (decimal Subtotal, List<OrderItem> Items) Compute(
        IReadOnlyList<OrderLineRequest> lines, IReadOnlyDictionary<Guid, Product> products, string lang)
    {
        if (lines.Count == 0) throw ProblemException.BadRequest("Order has no items");
        if (lines.Count > MaxLines) throw ProblemException.BadRequest($"At most {MaxLines} lines per order");

        var items = new List<OrderItem>();
        decimal subtotal = 0;
        foreach (var line in lines)
        {
            if (line.Qty is < 1 or > MaxQty) throw ProblemException.BadRequest($"Quantity must be 1..{MaxQty}");
            if (!products.TryGetValue(line.ProductId, out var p) || !p.Published)
                throw ProblemException.BadRequest("Product not available");
            if (!p.InStock) throw ProblemException.BadRequest($"Product out of stock: {p.NameUk}");

            var chosen = new List<(ProductOption Opt, ProductOptionValue Val)>();
            foreach (var valueId in line.OptionValueIds ?? [])
            {
                var hit = p.Options.SelectMany(o => o.Values.Select(v => (Opt: o, Val: v))).FirstOrDefault(x => x.Val.Id == valueId);
                if (hit.Val is null) throw ProblemException.BadRequest("Option value does not belong to the product");
                if (chosen.Any(c => c.Opt.Id == hit.Opt.Id)) throw ProblemException.BadRequest("Duplicate option");
                chosen.Add(hit);
            }
            // Every option on the product must be chosen (they change the price).
            var missing = p.Options.FirstOrDefault(o => o.Values.Count > 0 && chosen.All(c => c.Opt.Id != o.Id));
            if (missing is not null) throw ProblemException.BadRequest($"Option not chosen: {missing.NameUk}");

            var unit = p.Price + chosen.Sum(c => c.Val.PriceDelta);
            if (unit < 0) throw ProblemException.BadRequest("Invalid price");
            var lineTotal = unit * line.Qty;
            subtotal += lineTotal;
            items.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                ProductId = p.Id,
                ProductName = Lang.PickReq(p.NameUk, p.NameEn, lang),
                OptionsText = chosen.Count == 0 ? null
                    : string.Join("; ", chosen.Select(c => $"{Lang.PickReq(c.Opt.NameUk, c.Opt.NameEn, lang)}: {Lang.PickReq(c.Val.ValueUk, c.Val.ValueEn, lang)}")),
                Qty = line.Qty,
                UnitPrice = unit,
                LineTotal = lineTotal,
            });
        }
        return (subtotal, items);
    }
}
