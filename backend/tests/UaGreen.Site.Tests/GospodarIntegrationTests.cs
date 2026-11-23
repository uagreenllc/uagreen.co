using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UaGreen.Site.Api.Infrastructure.Auth;

namespace UaGreen.Site.Tests;

/// The site → Gospodar integration calls: header, path and body shape against a stub handler.
public class GospodarIntegrationTests
{
    private sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Req, string? Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls.Add((request, request.Content is null ? null : await request.Content.ReadAsStringAsync(ct)));
            return respond(request);
        }
    }

    private static (GospodarClient Client, Stub Stub) Make(string key, Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var stub = new Stub(respond);
        var http = new HttpClient(stub) { BaseAddress = new Uri("https://erp.test") };
        var client = new GospodarClient(http,
            Options.Create(new JwtOptions { SigningKey = new string('k', 32) }),
            Options.Create(new GospodarOptions { BaseUrl = "https://erp.test", ApiKey = key }),
            NullLogger<GospodarClient>.Instance);
        return (client, stub);
    }

    private static readonly GospodarLead Lead =
        new("c0ffee00-0000-0000-0000-000000000001", null, "+380501112233", null, "Консультація щодо аудиту ділянки", "Аудит вітру на ділянці");

    [Fact]
    public async Task PushLead_sends_api_key_and_lead_body()
    {
        var (client, stub) = Make("k1", _ => new HttpResponseMessage(HttpStatusCode.Created));

        Assert.True(await client.PushLeadAsync(Lead, CancellationToken.None));

        var (req, body) = Assert.Single(stub.Calls);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("/api/integration/leads", req.RequestUri!.AbsolutePath);
        Assert.Equal("k1", Assert.Single(req.Headers.GetValues("X-Api-Key")));
        using var json = JsonDocument.Parse(body!);
        Assert.Equal(Lead.ExternalId, json.RootElement.GetProperty("externalId").GetString());
        Assert.Equal("+380501112233", json.RootElement.GetProperty("phone").GetString());
        Assert.Equal("Консультація щодо аудиту ділянки", json.RootElement.GetProperty("comment").GetString());
    }

    [Fact]
    public async Task PushLead_skips_when_key_empty()
    {
        var (client, stub) = Make("", _ => new HttpResponseMessage(HttpStatusCode.Created));

        Assert.False(await client.PushLeadAsync(Lead, CancellationToken.None));
        Assert.Empty(stub.Calls);
    }

    [Fact]
    public async Task PushLead_returns_false_on_503()
    {
        var (client, _) = Make("k1", _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        Assert.False(await client.PushLeadAsync(Lead, CancellationToken.None));
    }
}
