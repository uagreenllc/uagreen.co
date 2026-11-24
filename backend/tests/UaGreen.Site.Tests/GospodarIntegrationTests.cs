using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UaGreen.Site.Api.Infrastructure.Auth;

namespace UaGreen.Site.Tests;

/// The site → Gospodar integration calls: header, path and body shape against a stub handler.
public class GospodarIntegrationTests
{
    private sealed class Stub(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Req, string? Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls.Add((request, request.Content is null ? null : await request.Content.ReadAsStringAsync(ct)));
            return await respond(request, ct);
        }
    }

    private static (GospodarClient Client, Stub Stub) Make(string key, Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        Make(key, (req, _) => Task.FromResult(respond(req)));

    private static (GospodarClient Client, Stub Stub) Make(string key, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond,
        TimeSpan? timeout = null)
    {
        var stub = new Stub(respond);
        var http = new HttpClient(stub) { BaseAddress = new Uri("https://erp.test") };
        var opts = new GospodarOptions { BaseUrl = "https://erp.test", IntegrationKey = key };
        if (timeout is { } t) opts.IntegrationTimeout = t;
        var client = new GospodarClient(http,
            Options.Create(new JwtOptions { SigningKey = new string('k', 32) }),
            Options.Create(opts),
            NullLogger<GospodarClient>.Instance);
        return (client, stub);
    }

    [Fact]
    public async Task GetAuditSlots_gives_up_after_the_integration_timeout()
    {
        // Gospodar hangs (or the resilience pipeline keeps retrying a dead host): the visitor must not wait for it.
        var (client, _) = Make("k1", async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return new HttpResponseMessage(); },
            TimeSpan.FromMilliseconds(200));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var dates = await client.GetAuditSlotsAsync(new DateOnly(2026, 12, 8), new DateOnly(2026, 12, 31), CancellationToken.None);

        Assert.Null(dates);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), $"took {sw.Elapsed}");
    }

    [Fact]
    public async Task GetAuditSlots_parses_dates_and_sends_key()
    {
        var (client, stub) = Make("k1", _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"dates":["2026-12-08","2026-12-10"]}""", System.Text.Encoding.UTF8, "application/json"),
        });

        var dates = await client.GetAuditSlotsAsync(new DateOnly(2026, 12, 8), new DateOnly(2026, 12, 31), CancellationToken.None);

        Assert.Equal([new DateOnly(2026, 12, 8), new DateOnly(2026, 12, 10)], dates);
        var (req, _) = Assert.Single(stub.Calls);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.Equal("/api/integration/audit-slots", req.RequestUri!.AbsolutePath);
        Assert.Equal("?from=2026-12-08&to=2026-12-31", req.RequestUri.Query);
        Assert.Equal("k1", Assert.Single(req.Headers.GetValues("X-Api-Key")));
    }

    [Fact]
    public async Task GetAuditSlots_returns_null_when_gospodar_throws()
    {
        var (client, _) = Make("k1", _ => throw new HttpRequestException("down"));

        Assert.Null(await client.GetAuditSlotsAsync(new DateOnly(2026, 12, 8), new DateOnly(2026, 12, 31), CancellationToken.None));
    }

    [Fact]
    public async Task BookAudit_parses_response()
    {
        var (client, stub) = Make("k1", _ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent(
                """{"dealId":"11111111-1111-1111-1111-111111111111","orderId":"22222222-2222-2222-2222-222222222222","tripId":"33333333-3333-3333-3333-333333333333","dateWasFree":false}""",
                System.Text.Encoding.UTF8, "application/json"),
        });
        var booking = new GospodarAuditBooking("ext-1", "Олена", "+380501112233", null, "с. Хроли", null,
            new DateOnly(2026, 12, 8), 15000m, new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc), "mono:inv_1",
            new DateTime(2026, 11, 23, 9, 58, 0, DateTimeKind.Utc));

        var r = await client.BookAuditAsync(booking, CancellationToken.None);

        Assert.NotNull(r);
        Assert.False(r.DateWasFree);
        Assert.Equal(Guid.Parse("33333333-3333-3333-3333-333333333333"), r.TripId);
        var (req, body) = Assert.Single(stub.Calls);
        Assert.Equal("/api/integration/audit-bookings", req.RequestUri!.AbsolutePath);
        using var json = JsonDocument.Parse(body!);
        Assert.Equal("ext-1", json.RootElement.GetProperty("externalId").GetString());
        Assert.Equal("2026-12-08", json.RootElement.GetProperty("date").GetString());
        Assert.Equal("mono:inv_1", json.RootElement.GetProperty("paymentRef").GetString());
        Assert.Equal(15000m, json.RootElement.GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task GetAuditSlots_returns_null_on_polly_rejection()
    {
        // The real pipeline throws this on its 30 s total timeout (and BrokenCircuitException on an open circuit).
        var (client, _) = Make("k1", _ => throw new Polly.Timeout.TimeoutRejectedException());

        Assert.Null(await client.GetAuditSlotsAsync(new DateOnly(2026, 12, 8), new DateOnly(2026, 12, 31), CancellationToken.None));
    }
}
