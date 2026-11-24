using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using UaGreen.Site.Api.Features.Chat;
using UaGreen.Site.Api.Infrastructure.Chat;

namespace UaGreen.Site.Tests;

public class ChatEndpointsTests
{
    private static async Task<(int Status, string? ContentType, string Body)> Run(ChatProxyResult r)
    {
        var ctx = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
        };
        ctx.Response.Body = new MemoryStream();
        await ChatEndpoints.ToResult(r).ExecuteAsync(ctx);
        ctx.Response.Body.Position = 0;
        return (ctx.Response.StatusCode, ctx.Response.ContentType, Encoding.UTF8.GetString(((MemoryStream)ctx.Response.Body).ToArray()));
    }

    [Fact]
    public async Task Unavailable_becomes_chat_unavailable()
    {
        var (status, _, body) = await Run(new ChatProxyResult(503, null));
        Assert.Equal(503, status);
        Assert.Contains("\"code\":\"CHAT_UNAVAILABLE\"", body);
        Assert.Contains("\"message\":\"Chat is unavailable\"", body);
    }

    [Fact]
    public async Task Gospodar_json_passes_through()
    {
        var (status, ct, body) = await Run(new ChatProxyResult(201, "{\"id\":\"m1\"}"));
        Assert.Equal(201, status);
        Assert.StartsWith("application/json", ct);
        Assert.Equal("{\"id\":\"m1\"}", body);
    }

    [Fact]
    public async Task Empty_body_keeps_the_status()
    {
        var (status, _, body) = await Run(new ChatProxyResult(204, null));
        Assert.Equal(204, status);
        Assert.Equal("", body);
    }

    [Fact]
    public async Task Gospodar_404_stays_404()
    {
        var (status, _, _) = await Run(new ChatProxyResult(404, null));
        Assert.Equal(404, status);
    }

    // nginx in front of Gospodar answers 502/504 with HTML while it is down or redeploying.
    [Fact]
    public async Task Gospodar_5xx_html_becomes_chat_unavailable()
    {
        var (status, ct, body) = await Run(new ChatProxyResult(502, "<html><body>502 Bad Gateway</body></html>"));
        Assert.Equal(503, status);
        Assert.StartsWith("application/json", ct);
        Assert.Contains("\"code\":\"CHAT_UNAVAILABLE\"", body);
    }

    // Key mismatch: the visitor gets the ContactForm fallback, not a dead chat.
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task Gospodar_auth_failure_becomes_chat_unavailable(int gospodarStatus)
    {
        var (status, _, body) = await Run(new ChatProxyResult(gospodarStatus, null));
        Assert.Equal(503, status);
        Assert.Contains("\"code\":\"CHAT_UNAVAILABLE\"", body);
    }

    [Fact]
    public async Task No_content_has_no_body()
    {
        var (status, ct, body) = await Run(new ChatProxyResult(204, ""));
        Assert.Equal(204, status);
        Assert.Null(ct);
        Assert.Equal("", body);
    }

    [Fact]
    public void Every_chat_endpoint_caps_the_request_body_at_16_kb()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddMemoryCache();
        builder.Services.Configure<SiteChatOptions>(_ => { });
        builder.Services.AddHttpClient<GospodarChatClient>();
        var app = builder.Build();
        ChatEndpoints.Map(app);

        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(d => d.Endpoints)
            .OfType<RouteEndpoint>().Where(e => e.RoutePattern.RawText!.StartsWith("/api/chat")).ToList();
        Assert.Equal(6, endpoints.Count);
        Assert.All(endpoints, e => Assert.Equal(16 * 1024, e.Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize));
    }
}
