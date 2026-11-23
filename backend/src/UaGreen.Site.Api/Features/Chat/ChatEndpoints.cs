using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Infrastructure.Chat;
using UaGreen.Site.Api.Infrastructure.RateLimiting;

namespace UaGreen.Site.Api.Features.Chat;

public sealed record StartChatRequest(string? Text, string? Lang, string? PageUrl, Guid? QuestionId, string? Website);
public sealed record SendChatMessageRequest(string? Text);
public sealed record ChatContactRequest(string? Name, string? Phone);

/// Public chat widget API: a thin proxy to Gospodar. Text/lang/phone validation lives there.
public static class ChatEndpoints
{
    private const string TokenHeader = "X-Chat-Token";

    public static void Map(IEndpointRouteBuilder app)
    {
        // Chat bodies are a few KB at most (text <= 2000 chars); Kestrel's default is 30 MB.
        var g = app.MapGroup("/api/chat").AllowAnonymous().DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        g.MapGet("/questions", async (string? lang, IOptions<SiteChatOptions> opts, IMemoryCache cache, GospodarChatClient chat, CancellationToken ct) =>
        {
            if (!opts.Value.Enabled) return Results.Json(new { enabled = false, questions = Array.Empty<object>() });
            lang = lang == "en" ? "en" : "uk";
            var key = "chat-questions:" + lang;
            if (cache.TryGetValue(key, out object? hit)) return Results.Json(hit);

            var r = await chat.SendAsync(HttpMethod.Get, "/questions?lang=" + lang, null, null, ct);
            if (r.Status != 200 || r.Json is null) return Results.Json(new { enabled = true, questions = Array.Empty<object>() });
            try
            {
                var questions = (JsonNode.Parse(r.Json) as JsonArray ?? [])
                    .Select(q => new { id = (string?)q?["id"], text = (string?)q?["text"] }).ToList();
                var body = new { enabled = true, questions };
                cache.Set(key, body, TimeSpan.FromMinutes(5));
                return Results.Json(body);
            }
            catch (System.Text.Json.JsonException)
            {
                return Results.Json(new { enabled = true, questions = Array.Empty<object>() });
            }
        }).RequireRateLimiting(RateLimitingExtensions.Chat);

        g.MapPost("/questions/{id:guid}/impressions", (Guid id, IOptions<SiteChatOptions> opts, GospodarChatClient chat, CancellationToken ct) =>
            Proxy(opts, () => chat.SendAsync(HttpMethod.Post, $"/questions/{id}/impressions", null, null, ct)))
            .RequireRateLimiting(RateLimitingExtensions.Chat);

        g.MapPost("/conversations", (StartChatRequest req, IOptions<SiteChatOptions> opts, GospodarChatClient chat, CancellationToken ct) =>
        {
            if (!string.IsNullOrEmpty(req.Website)) return Task.FromResult(Results.NoContent()); // honeypot
            return Proxy(opts, () => chat.SendAsync(HttpMethod.Post, "/conversations",
                new { req.Text, req.Lang, req.PageUrl, req.QuestionId }, null, ct));
        }).RequireRateLimiting(RateLimitingExtensions.ChatStart);

        g.MapGet("/conversations/{id:guid}/messages", (Guid id, HttpContext ctx, IOptions<SiteChatOptions> opts, GospodarChatClient chat, CancellationToken ct) =>
            Proxy(opts, () => chat.SendAsync(HttpMethod.Get, $"/conversations/{id}/messages", null, Token(ctx), ct)))
            .RequireRateLimiting(RateLimitingExtensions.Chat);

        g.MapPost("/conversations/{id:guid}/messages", (Guid id, SendChatMessageRequest req, HttpContext ctx, IOptions<SiteChatOptions> opts, GospodarChatClient chat, CancellationToken ct) =>
            Proxy(opts, () => chat.SendAsync(HttpMethod.Post, $"/conversations/{id}/messages", new { req.Text }, Token(ctx), ct)))
            .RequireRateLimiting(RateLimitingExtensions.Chat);

        g.MapPut("/conversations/{id:guid}/contact", (Guid id, ChatContactRequest req, HttpContext ctx, IOptions<SiteChatOptions> opts, GospodarChatClient chat, CancellationToken ct) =>
            Proxy(opts, () => chat.SendAsync(HttpMethod.Put, $"/conversations/{id}/contact", new { req.Name, req.Phone }, Token(ctx), ct)))
            .RequireRateLimiting(RateLimitingExtensions.Chat);
    }

    private static string? Token(HttpContext ctx) => ctx.Request.Headers[TokenHeader].FirstOrDefault();

    // Chat disabled (no key) -> 404, as if the endpoints did not exist.
    private static async Task<IResult> Proxy(IOptions<SiteChatOptions> opts, Func<Task<ChatProxyResult>> call) =>
        opts.Value.Enabled ? ToResult(await call()) : Results.NotFound();

    // public (not internal): the test project has no InternalsVisibleTo.
    // Gospodar down (nginx 502/504 HTML) or a key mismatch (401/403) is "unavailable" to the visitor:
    // the widget then offers the ContactForm instead of a chat that silently fails.
    public static IResult ToResult(ChatProxyResult r) =>
        r.Status >= 500 || r.Status is 401 or 403
            ? Results.Json(new ApiError("CHAT_UNAVAILABLE", "Chat is unavailable"), statusCode: 503)
        : string.IsNullOrEmpty(r.Json) ? Results.StatusCode(r.Status)
        : Results.Content(r.Json, "application/json", statusCode: r.Status);
}
