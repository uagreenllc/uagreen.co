using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Features.Forms;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Persistence;

namespace UaGreen.Site.Tests;

public class CrmLeadSyncTests
{
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

    private static ContactRequest Request(string name, DateTime createdAt, Guid? dealId = null, string message = "Хочу вітряк") => new()
    {
        Id = Guid.NewGuid(), Name = name, Phone = "0501112233", Message = message, CreatedAt = createdAt, CrmDealId = dealId,
    };

    [Fact]
    public async Task Pushes_only_fresh_unsent_requests_and_keeps_failed_ones_for_retry()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("crm-" + Guid.NewGuid()).Options);
        var fresh = Request("Олена", DateTime.UtcNow, message: new string('я', 1500));
        var failing = Request("Збій", DateTime.UtcNow);
        db.ContactRequests.AddRange(fresh, failing,
            Request("Вже в CRM", DateTime.UtcNow, Guid.NewGuid()),
            Request("Тиждень тому", DateTime.UtcNow.AddDays(-7)));
        await db.SaveChangesAsync();

        var dealId = Guid.NewGuid();
        var fake = new FakeGospodar(body => body.GetProperty("name").GetString() == "Збій"
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent($"{{\"dealId\":\"{dealId}\"}}") });
        var client = new GospodarClient(new HttpClient(fake) { BaseAddress = new Uri("http://gospodar") },
            Options.Create(new JwtOptions()), Options.Create(new GospodarOptions { IntegrationKey = "company-key" }),
            NullLogger<GospodarClient>.Instance);

        await CrmLeadSync.PushAsync(db, client, NullLogger.Instance, CancellationToken.None);

        Assert.Equal(2, fake.Calls.Count);
        var sent = fake.Calls.Single(c => c.Body.GetProperty("name").GetString() == "Олена");
        Assert.Equal("company-key", sent.Key);
        Assert.Equal("Сайт uagreen.co", sent.Body.GetProperty("source").GetString());
        Assert.Equal(fresh.Id.ToString("N"), sent.Body.GetProperty("externalId").GetString());
        Assert.Equal(1000, sent.Body.GetProperty("comment").GetString()!.Length);
        Assert.Equal(dealId, (await db.ContactRequests.SingleAsync(c => c.Id == fresh.Id)).CrmDealId);
        Assert.Null((await db.ContactRequests.SingleAsync(c => c.Id == failing.Id)).CrmDealId);
    }
}
