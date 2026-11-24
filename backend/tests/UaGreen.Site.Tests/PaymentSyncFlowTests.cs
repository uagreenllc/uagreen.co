using System.Diagnostics;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Features.Orders;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Email;
using UaGreen.Site.Api.Infrastructure.Payments;
using UaGreen.Site.Api.Infrastructure.Persistence;

namespace UaGreen.Site.Tests;

/// PaymentSync end to end over an in-memory DB, a fake monobank and a fake Gospodar.
public class PaymentSyncFlowTests
{
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            return respond(r, ct);
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static readonly IOptions<AppOptions> App = Options.Create(new AppOptions { PublicBaseUrl = "https://uagreen.co" });

    private sealed record World(ServiceProvider Services, Handler Bank, Handler Gospodar, Guid OrderId);

    private static async Task<World> MakeAsync(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> bank, bool audit)
    {
        var bankHandler = new Handler(bank);
        var gospodarHandler = new Handler((_, _) => Task.FromResult(Json(
            """{"dealId":"11111111-1111-1111-1111-111111111111","orderId":"22222222-2222-2222-2222-222222222222","tripId":"33333333-3333-3333-3333-333333333333","dateWasFree":true}""",
            HttpStatusCode.Created)));
        var dbName = Guid.NewGuid().ToString();
        var s = new ServiceCollection();
        s.AddLogging();
        s.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        s.AddSingleton(Options.Create(new SmtpOptions()));
        s.AddSingleton(Options.Create(new NotificationOptions()));
        s.AddScoped<Notifier>();
        s.AddSingleton(new GospodarClient(new HttpClient(gospodarHandler) { BaseAddress = new Uri("https://erp.test") },
            Options.Create(new JwtOptions { SigningKey = new string('k', 32) }),
            Options.Create(new GospodarOptions { BaseUrl = "https://erp.test", IntegrationKey = "k1" }),
            NullLogger<GospodarClient>.Instance));
        s.AddSingleton(new MonobankAcquiring(new HttpClient(bankHandler) { BaseAddress = new Uri(MonobankAcquiring.BaseUrl) },
            Options.Create(new MonobankOptions { Token = "t" }), App));
        s.AddSingleton(new MonoPartsClient(new HttpClient(bankHandler) { BaseAddress = new Uri("https://parts.test") },
            Options.Create(new MonoPartsOptions { StoreId = "s", Secret = "x" }), App));
        s.AddScoped<PaymentSync>();
        var sp = s.BuildServiceProvider();

        var id = Guid.NewGuid();
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Orders.Add(new Order
            {
                Id = id, Number = 7, Status = OrderStatus.AwaitingPayment, PaymentMethod = PaymentMethod.Card, PaymentRef = "inv_1",
                CustomerName = "Олена", Phone = "+380501112233", Total = 15000m, Subtotal = 15000m,
                AuditDate = audit ? new DateOnly(2026, 12, 8) : null, AuditAddress = audit ? "с. Хроли" : null,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, OfferAcceptedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        return new World(sp, bankHandler, gospodarHandler, id);
    }

    private static async Task RefreshInNewScopeAsync(World w, bool ifStale)
    {
        using var scope = w.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sync = scope.ServiceProvider.GetRequiredService<PaymentSync>();
        var o = await db.Orders.FirstAsync(x => x.Id == w.OrderId);
        if (ifStale) await sync.RefreshIfStaleAsync(o, CancellationToken.None);
        else await sync.RefreshAsync(o, CancellationToken.None);
    }

    private static async Task<Order> LoadAsync(World w)
    {
        using var scope = w.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Orders.AsNoTracking().FirstAsync(x => x.Id == w.OrderId);
    }

    /// The customer's order page must not wait for a slow bank: it answers from the DB within the page budget.
    [Fact]
    public async Task Page_refresh_gives_up_on_a_slow_bank()
    {
        var w = await MakeAsync(async (_, ct) => { await Task.Delay(TimeSpan.FromSeconds(15), ct); return Json("{}"); }, audit: false);

        var sw = Stopwatch.StartNew();
        await RefreshInNewScopeAsync(w, ifStale: true);
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(4), $"took {sw.Elapsed}");
        Assert.Equal(OrderStatus.AwaitingPayment, (await LoadAsync(w)).Status);
    }

    /// The customer paid an older invoice of the order (second tab): the webhook about it still marks the order paid.
    [Fact]
    public async Task Paid_older_invoice_marks_the_order_paid()
    {
        Guid id = default;
        var w = await MakeAsync((r, _) => Task.FromResult(r.RequestUri!.Query.Contains("inv_old")
            ? Json($$"""{"invoiceId":"inv_old","status":"success","amount":1500000,"ccy":980,"reference":"{{id}}"}""")
            : Json($$"""{"invoiceId":"inv_1","status":"created","amount":1500000,"ccy":980,"reference":"{{id}}"}""")), audit: false);
        id = w.OrderId;

        using (var scope = w.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var o = await db.Orders.FirstAsync(x => x.Id == id);
            await scope.ServiceProvider.GetRequiredService<PaymentSync>().RefreshAsync(o, CancellationToken.None, "inv_old");
        }

        var paid = await LoadAsync(w);
        Assert.Equal(OrderStatus.Paid, paid.Status);
        Assert.Equal("inv_old", paid.PaymentRef);
    }

    /// The webhook and the customer's page refresh land together after an audit payment: one Gospodar booking.
    [Fact]
    public async Task Concurrent_refreshes_book_the_audit_once()
    {
        Guid id = default;
        var w = await MakeAsync(async (_, ct) =>
        {
            await Task.Delay(50, ct);
            return Json($$"""{"invoiceId":"inv_1","status":"success","amount":1500000,"ccy":980,"reference":"{{id}}"}""");
        }, audit: true);
        id = w.OrderId;

        await Task.WhenAll(RefreshInNewScopeAsync(w, ifStale: false), RefreshInNewScopeAsync(w, ifStale: false));

        // Side effects run after the response: wait for the booking, then make sure no second one follows.
        for (var i = 0; i < 100 && (await LoadAsync(w)).GospodarBookedAt is null; i++) await Task.Delay(50);
        await Task.Delay(300);
        var o = await LoadAsync(w);
        Assert.Equal(OrderStatus.Paid, o.Status);
        Assert.NotNull(o.GospodarBookedAt);
        Assert.Equal(1, w.Gospodar.Calls);
    }
}
