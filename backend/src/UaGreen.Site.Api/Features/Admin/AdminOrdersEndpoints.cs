using Microsoft.EntityFrameworkCore;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Crm;
using UaGreen.Site.Api.Infrastructure.Persistence;
using static UaGreen.Site.Api.Features.Admin.AdminCommon;

namespace UaGreen.Site.Api.Features.Admin;

public sealed record OrderItemAdminDto(Guid Id, Guid? ProductId, string ProductName, string? OptionsText, int Qty, decimal UnitPrice, decimal LineTotal);
public sealed record OrderEventDto(DateTime At, string Actor, string Text);
public sealed record CrmStateDto(Guid? DealId, DateTime? SyncedAt, string? Error, int Attempts);
public sealed record OrderAdminDto(
    Guid Id, int Number, string Status, string CustomerName, string Phone, string? Email,
    string DeliveryMethod, string? DeliveryCity, string? DeliveryWarehouse, string? Comment, string PaymentMethod,
    decimal Subtotal, decimal DeliveryCost, decimal Total, string? PaymentStatus, DateTime? PaidAt, DateTime CreatedAt, DateTime UpdatedAt,
    IReadOnlyList<OrderItemAdminDto> Items, string? AdminNote, CrmStateDto Crm, IReadOnlyList<OrderEventDto>? Events);
public sealed record OrderStatusUpdate(string Status);
public sealed record OrderItemEdit(Guid Id, int Qty, decimal UnitPrice);
/// Full edit of what a manager may correct after checkout. Items: rows not listed are removed.
public sealed record OrderUpdate(
    string CustomerName, string Phone, string? Email, string DeliveryMethod, string? DeliveryCity, string? DeliveryWarehouse,
    string? Comment, string? AdminNote, decimal DeliveryCost, List<OrderItemEdit> Items);

public static class AdminOrdersEndpoints
{
    public static readonly Dictionary<OrderStatus, string> StatusUk = new()
    {
        [OrderStatus.New] = "Нове", [OrderStatus.AwaitingPayment] = "Очікує оплату", [OrderStatus.Paid] = "Оплачено",
        [OrderStatus.InvoiceSent] = "Рахунок надіслано", [OrderStatus.Credit] = "Кредит", [OrderStatus.Shipped] = "Відправлено",
        [OrderStatus.Cancelled] = "Скасовано",
    };

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin/orders").RequireAuthorization(Policies.Sales);

        // q: number, name, phone or email. crm: failed | pending
        g.MapGet("/", async (string? status, string? q, string? crm, int? page, int? pageSize, AppDbContext db, CancellationToken ct) =>
        {
            var query = db.Orders.AsNoTracking().Include(o => o.Items).AsSplitQuery().AsQueryable();
            if (!string.IsNullOrEmpty(status))
            {
                if (!Enum.TryParse<OrderStatus>(status, true, out var st)) throw ProblemException.BadRequest("Невідомий статус");
                query = query.Where(o => o.Status == st);
            }
            if (!string.IsNullOrWhiteSpace(q))
            {
                var t = q.Trim().TrimStart('№', '#');
                query = int.TryParse(t, out var n)
                    ? query.Where(o => o.Number == n || o.Phone.Contains(t))
                    : query.Where(o => EF.Functions.ILike(o.CustomerName, Like(t)) || (o.Email != null && EF.Functions.ILike(o.Email, Like(t))) || o.Phone.Contains(t));
            }
            if (crm == "failed") query = query.Where(o => o.CrmDealId == null && o.CrmError != null);
            if (crm == "pending") query = query.Where(o => o.CrmDealId == null && o.CrmError == null);
            return await PageAsync(query.OrderByDescending(o => o.CreatedAt), page, pageSize, o => ToDto(o, null), ct);
        });

        g.MapGet("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
            ToDto(await LoadAsync(db, id, ct, track: false)));

        g.MapPatch("/{id:guid}/status", async (Guid id, OrderStatusUpdate req, HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            if (!Enum.TryParse<OrderStatus>(req.Status, true, out var st)) throw ProblemException.BadRequest("Невідомий статус");
            var o = await LoadAsync(db, id, ct);
            if (o.Status != st)
            {
                Log(db, o, http, $"Статус: {StatusUk[o.Status]} → {StatusUk[st]}");
                o.Status = st;
                if (st == OrderStatus.Paid && o.PaidAt is null) o.PaidAt = DateTime.UtcNow;
                o.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            return Results.Ok(ToDto(await LoadAsync(db, id, ct, track: false)));
        });

        g.MapPut("/{id:guid}", async (Guid id, OrderUpdate req, HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var o = await LoadAsync(db, id, ct);
            if (string.IsNullOrWhiteSpace(req.CustomerName)) throw ProblemException.BadRequest("Вкажіть ім'я клієнта");
            if (string.IsNullOrWhiteSpace(req.Phone)) throw ProblemException.BadRequest("Вкажіть телефон");
            if (!Enum.TryParse<DeliveryMethod>(req.DeliveryMethod, true, out var dm)) throw ProblemException.BadRequest("Невідомий спосіб доставки");
            if (req.DeliveryCost < 0) throw ProblemException.BadRequest("Вартість доставки не може бути від'ємною");

            var changes = new List<string>();
            void Set<T>(string label, T old, T now, Action apply)
            {
                if (EqualityComparer<T>.Default.Equals(old, now)) return;
                apply();
                changes.Add($"{label}: {Show(old)} → {Show(now)}");
            }
            Set("Клієнт", o.CustomerName, req.CustomerName.Trim(), () => o.CustomerName = req.CustomerName.Trim());
            Set("Телефон", o.Phone, req.Phone.Trim(), () => o.Phone = req.Phone.Trim());
            Set("Email", o.Email, Clean(req.Email), () => o.Email = Clean(req.Email));
            Set("Доставка", o.DeliveryMethod, dm, () => o.DeliveryMethod = dm);
            Set("Місто", o.DeliveryCity, Clean(req.DeliveryCity), () => o.DeliveryCity = Clean(req.DeliveryCity));
            Set("Відділення", o.DeliveryWarehouse, Clean(req.DeliveryWarehouse), () => o.DeliveryWarehouse = Clean(req.DeliveryWarehouse));
            Set("Коментар клієнта", o.Comment, Clean(req.Comment), () => o.Comment = Clean(req.Comment));
            if (o.AdminNote != Clean(req.AdminNote)) { o.AdminNote = Clean(req.AdminNote); changes.Add("Оновлено нотатку"); }
            Set("Вартість доставки", o.DeliveryCost, req.DeliveryCost, () => o.DeliveryCost = req.DeliveryCost);

            var wanted = req.Items.ToDictionary(i => i.Id);
            foreach (var item in o.Items.ToList())
            {
                if (!wanted.TryGetValue(item.Id, out var w))
                {
                    db.OrderItems.Remove(item);
                    o.Items.Remove(item);
                    changes.Add($"Видалено позицію «{item.ProductName}»");
                    continue;
                }
                if (w.Qty < 1 || w.Qty > 1000) throw ProblemException.BadRequest("Кількість — від 1 до 1000");
                if (w.UnitPrice < 0) throw ProblemException.BadRequest("Ціна не може бути від'ємною");
                if (w.Qty != item.Qty || w.UnitPrice != item.UnitPrice)
                {
                    changes.Add($"«{item.ProductName}»: {item.Qty} × {item.UnitPrice:0.##} → {w.Qty} × {w.UnitPrice:0.##}");
                    item.Qty = w.Qty;
                    item.UnitPrice = w.UnitPrice;
                    item.LineTotal = w.Qty * w.UnitPrice;
                }
            }
            if (o.Items.Count == 0) throw ProblemException.BadRequest("У замовленні має лишитися хоча б одна позиція");
            o.Subtotal = o.Items.Sum(i => i.LineTotal);
            o.Total = o.Subtotal + o.DeliveryCost;

            if (changes.Count > 0)
            {
                Log(db, o, http, string.Join("; ", changes));
                o.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            return Results.Ok(ToDto(await LoadAsync(db, id, ct, track: false)));
        });

        // Push to Gospodar CRM now (manual retry, ignores the "since" cut-off).
        g.MapPost("/{id:guid}/crm", async (Guid id, CrmSync sync, AppDbContext db, CancellationToken ct) =>
        {
            var o = await LoadAsync(db, id, ct);
            await sync.PushAsync(o, ct);
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToDto(await LoadAsync(db, id, ct, track: false)));
        });
    }

    private static string Show<T>(T v) => v switch
    {
        null => "—",
        string s when s.Length == 0 => "—",
        DeliveryMethod d => d switch { DeliveryMethod.NovaPoshta => "Нова пошта", DeliveryMethod.Ukrposhta => "Укрпошта", _ => "Самовивіз" },
        decimal m => m.ToString("0.##"),
        _ => v.ToString()!,
    };

    private static void Log(AppDbContext db, Order o, HttpContext http, string text)
        => db.OrderEvents.Add(new OrderEvent { Id = Guid.NewGuid(), OrderId = o.Id, At = DateTime.UtcNow, Actor = Actor(http.User), Text = text.Length > 1000 ? text[..1000] : text });

    private static async Task<Order> LoadAsync(AppDbContext db, Guid id, CancellationToken ct, bool track = true)
    {
        var q = db.Orders.Include(o => o.Items).Include(o => o.Events).AsSplitQuery();
        return await (track ? q : q.AsNoTracking()).FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw ProblemException.NotFound("Замовлення не знайдено");
    }

    private static OrderAdminDto ToDto(Order o) => ToDto(o, o.Events.OrderByDescending(e => e.At).Select(e => new OrderEventDto(e.At, e.Actor, e.Text)).ToList());

    private static OrderAdminDto ToDto(Order o, IReadOnlyList<OrderEventDto>? events) => new(
        o.Id, o.Number, o.Status.ToString(), o.CustomerName, o.Phone, o.Email,
        o.DeliveryMethod.ToString(), o.DeliveryCity, o.DeliveryWarehouse, o.Comment, o.PaymentMethod.ToString(),
        o.Subtotal, o.DeliveryCost, o.Total, o.PaymentStatus, o.PaidAt, o.CreatedAt, o.UpdatedAt,
        o.Items.Select(i => new OrderItemAdminDto(i.Id, i.ProductId, i.ProductName, i.OptionsText, i.Qty, i.UnitPrice, i.LineTotal)).ToList(),
        o.AdminNote, new CrmStateDto(o.CrmDealId, o.CrmSyncedAt, o.CrmError, o.CrmAttempts), events);
}
