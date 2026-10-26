using Microsoft.EntityFrameworkCore;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Features.Orders;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Persistence;

namespace UaGreen.Site.Api.Features.Admin;

public sealed record OrderAdminDto(
    Guid Id, int Number, string Status, string CustomerName, string Phone, string? Email,
    string DeliveryMethod, string? DeliveryCity, string? DeliveryWarehouse, string? Comment, string PaymentMethod,
    decimal Subtotal, decimal DeliveryCost, decimal Total, string? LiqPayStatus, DateTime? PaidAt, DateTime CreatedAt, DateTime UpdatedAt,
    IReadOnlyList<OrderItemDto> Items);
public sealed record OrderStatusUpdate(string Status);

public static class AdminOrdersEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin/orders").RequireAuthorization(Policies.Sales);

        g.MapGet("/", async (string? status, int? page, int? pageSize, AppDbContext db, CancellationToken ct) =>
        {
            var pn = Math.Max(1, page ?? 1);
            var ps = Math.Clamp(pageSize ?? 50, 1, 200);
            var q = db.Orders.AsNoTracking().Include(o => o.Items).AsQueryable();
            if (!string.IsNullOrEmpty(status))
            {
                if (!Enum.TryParse<OrderStatus>(status, true, out var st)) throw ProblemException.BadRequest("Unknown status");
                q = q.Where(o => o.Status == st);
            }
            var total = await q.CountAsync(ct);
            var rows = await q.OrderByDescending(o => o.CreatedAt).Skip((pn - 1) * ps).Take(ps).ToListAsync(ct);
            return new Page<OrderAdminDto>(rows.Select(ToDto).ToList(), total, pn, ps);
        });

        g.MapGet("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
            ToDto(await db.Orders.AsNoTracking().Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct)
                ?? throw ProblemException.NotFound("Order not found")));

        g.MapPatch("/{id:guid}/status", async (Guid id, OrderStatusUpdate req, AppDbContext db, CancellationToken ct) =>
        {
            if (!Enum.TryParse<OrderStatus>(req.Status, true, out var st)) throw ProblemException.BadRequest("Unknown status");
            var o = await db.Orders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw ProblemException.NotFound("Order not found");
            o.Status = st;
            if (st == OrderStatus.Paid && o.PaidAt is null) o.PaidAt = DateTime.UtcNow;
            o.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToDto(o));
        });
    }

    private static OrderAdminDto ToDto(Order o) => new(
        o.Id, o.Number, o.Status.ToString(), o.CustomerName, o.Phone, o.Email,
        o.DeliveryMethod.ToString(), o.DeliveryCity, o.DeliveryWarehouse, o.Comment, o.PaymentMethod.ToString(),
        o.Subtotal, o.DeliveryCost, o.Total, o.LiqPayStatus, o.PaidAt, o.CreatedAt, o.UpdatedAt,
        o.Items.Select(i => new OrderItemDto(i.ProductName, i.OptionsText, i.Qty, i.UnitPrice, i.LineTotal)).ToList());
}
