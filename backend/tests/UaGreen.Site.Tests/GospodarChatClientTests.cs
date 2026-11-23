using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UaGreen.Site.Api.Infrastructure.Chat;

namespace UaGreen.Site.Tests;

public class GospodarChatClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Request;
        public string? Body;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return respond(request);
        }
    }

    private static (GospodarChatClient, StubHandler) Make(Func<HttpRequestMessage, HttpResponseMessage>? respond = null, string key = "k")
    {
        var stub = new StubHandler(respond ?? (_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var http = new HttpClient(stub) { BaseAddress = new Uri("https://erp.test") };
        return (new GospodarChatClient(http, Options.Create(new SiteChatOptions { ApiKey = key }), NullLogger<GospodarChatClient>.Instance), stub);
    }

    [Fact]
    public async Task Sends_key_token_and_path()
    {
        var (c, stub) = Make();
        await c.SendAsync(HttpMethod.Post, "/conversations/abc/messages", new { text = "Привіт" }, "tok", default);
        var r = stub.Request!;
        Assert.Equal(HttpMethod.Post, r.Method);
        Assert.Equal("https://erp.test/api/site-chat/conversations/abc/messages", r.RequestUri!.ToString());
        Assert.Equal("k", r.Headers.GetValues("X-Api-Key").Single());
        Assert.Equal("tok", r.Headers.GetValues("X-Chat-Token").Single());
        Assert.Equal("Привіт", System.Text.Json.JsonDocument.Parse(stub.Body!).RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Disabled_sends_nothing()
    {
        var (c, stub) = Make(key: "");
        var res = await c.SendAsync(HttpMethod.Get, "/questions", null, null, default);
        Assert.Null(stub.Request);
        Assert.Equal(503, res.Status);
        Assert.Null(res.Json);
    }

    [Fact]
    public async Task No_token_no_header()
    {
        var (c, stub) = Make();
        await c.SendAsync(HttpMethod.Get, "/questions", null, null, default);
        Assert.False(stub.Request!.Headers.Contains("X-Chat-Token"));
    }

    [Fact]
    public async Task Passes_status_and_body_through()
    {
        var (c, _) = Make(_ => new HttpResponseMessage(HttpStatusCode.Created)
            { Content = new StringContent("{\"id\":\"m1\"}", Encoding.UTF8, "application/json") });
        var res = await c.SendAsync(HttpMethod.Post, "/x", new { }, null, default);
        Assert.Equal(201, res.Status);
        Assert.Equal("{\"id\":\"m1\"}", res.Json);
    }

    [Fact]
    public async Task Network_failure_is_503()
    {
        var (c, _) = Make(_ => throw new HttpRequestException("down"));
        var res = await c.SendAsync(HttpMethod.Get, "/x", null, null, default);
        Assert.Equal(503, res.Status);
        Assert.Null(res.Json);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_swallowed()
    {
        var (c, _) = Make(_ => throw new TaskCanceledException());
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => c.SendAsync(HttpMethod.Get, "/x", null, null, cts.Token));
    }
}
