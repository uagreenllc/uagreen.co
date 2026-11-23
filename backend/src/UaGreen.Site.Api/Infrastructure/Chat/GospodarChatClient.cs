using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UaGreen.Site.Api.Infrastructure.Chat;

public sealed record ChatProxyResult(int Status, string? Json);

/// Thin proxy to Gospodar's /api/site-chat/*; status and body pass through as-is.
public sealed class GospodarChatClient(HttpClient http, IOptions<SiteChatOptions> opts, ILogger<GospodarChatClient> log)
{
    public async Task<ChatProxyResult> SendAsync(HttpMethod method, string path, object? body, string? chatToken, CancellationToken ct)
    {
        if (!opts.Value.Enabled) return new ChatProxyResult(503, null); // no key: never call Gospodar
        try
        {
            using var req = new HttpRequestMessage(method, "/api/site-chat" + path);
            req.Headers.Add("X-Api-Key", opts.Value.ApiKey);
            if (!string.IsNullOrEmpty(chatToken)) req.Headers.Add("X-Chat-Token", chatToken);
            if (body is not null) req.Content = JsonContent.Create(body);
            using var resp = await http.SendAsync(req, ct);
            return new ChatProxyResult((int)resp.StatusCode, await resp.Content.ReadAsStringAsync(ct));
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            log.LogWarning(ex, "Gospodar site-chat {Method} {Path} failed", method, path);
            // network, timeouts, open circuit (Polly) -> 503
            return new ChatProxyResult(503, null);
        }
    }
}
