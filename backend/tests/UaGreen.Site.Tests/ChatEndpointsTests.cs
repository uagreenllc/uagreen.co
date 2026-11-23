using System.Text;
using Microsoft.AspNetCore.Http;
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
}
