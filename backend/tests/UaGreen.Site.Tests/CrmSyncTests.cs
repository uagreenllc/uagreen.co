using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Crm;
using UaGreen.Site.Api.Infrastructure.Persistence;

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

    private sealed class FakeGospodar(Func<JsonElement, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(string? Key, JsonElement Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            var body = JsonDocument.Parse(await req.Content!.ReadAsStringAsync(ct)).RootElement;
            Calls.Add((req.Headers.GetValues("X-Api-Key").Single(), body));
            return respond(body);
        }
    }

    private sealed class Factory(HttpMessageHandler h) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(h, disposeHandler: false);
    }

    private static ContactRequest Request(string name, DateTime createdAt, Guid? dealId = null, string message = "Хочу вітряк") => new()
    {
        Id = Guid.NewGuid(), Name = name, Phone = "0501112233", Message = message, CreatedAt = createdAt, CrmDealId = dealId,
    };

    [Fact]
    public async Task Tick_pushes_fresh_requests_keeps_failed_for_retry_and_leaves_orders_until_switched_on()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase("crm-" + Guid.NewGuid()).Options);
        var fresh = Request("Олена", DateTime.UtcNow, message: new string('я', 1500));
        var failing = Request("Збій", DateTime.UtcNow);
        db.ContactRequests.AddRange(fresh, failing, Request("Вже в CRM", DateTime.UtcNow, Guid.NewGuid()), Request("Тиждень тому", DateTime.UtcNow.AddDays(-7)));
        db.Orders.Add(new Order { Id = Guid.NewGuid(), Number = 7, CustomerName = "Іван", Phone = "0501112233", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var dealId = Guid.NewGuid();
        var fake = new FakeGospodar(body => body.GetProperty("name").GetString() == "Збій"
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{\"error\":\"no_responsible_user\"}") }
            : new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent($"{{\"dealId\":\"{dealId}\"}}") });
        var sync = new CrmSync(new Factory(fake), Options.Create(new GospodarOptions { BaseUrl = "http://gospodar", IntegrationKey = "company-key" }),
            NullLogger<CrmSync>.Instance);

        await CrmSyncWorker.TickAsync(db, sync, CancellationToken.None);

        Assert.Equal(2, fake.Calls.Count); // orders feed is off by default, old and sent requests are skipped
        var sent = fake.Calls.Single(c => c.Body.GetProperty("name").GetString() == "Олена");
        Assert.Equal("company-key", sent.Key);
        Assert.Equal("Сайт uagreen.co", sent.Body.GetProperty("source").GetString());
        Assert.Equal(fresh.Id.ToString("N"), sent.Body.GetProperty("externalId").GetString());
        Assert.Equal(1000, sent.Body.GetProperty("comment").GetString()!.Length);
        Assert.Equal(dealId, (await db.ContactRequests.SingleAsync(c => c.Id == fresh.Id)).CrmDealId);
        var f = await db.ContactRequests.SingleAsync(c => c.Id == failing.Id);
        Assert.Null(f.CrmDealId);
        Assert.Equal(1, f.CrmAttempts);
        Assert.Contains("менеджера", f.CrmError);

        // Switched on: the order goes; the failed request waits for its backoff.
        db.Settings.Add(new Setting { Key = CrmSync.OrdersKey, Value = DateTime.UtcNow.AddMinutes(-5).ToString("O") });
        await db.SaveChangesAsync();
        await CrmSyncWorker.TickAsync(db, sync, CancellationToken.None);
        Assert.Equal(3, fake.Calls.Count);
        Assert.Equal("site-order-7", fake.Calls[2].Body.GetProperty("externalId").GetString());
    }

    [Fact]
    public void Since_parses_the_stored_switch_time()
    {
        Assert.Null(CrmSync.Since(null));
        Assert.Null(CrmSync.Since("on"));
        Assert.Equal(new DateTime(2026, 11, 23, 4, 0, 0, DateTimeKind.Utc), CrmSync.Since("2026-11-23T04:00:00.0000000Z"));
    }
}
