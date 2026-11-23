using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using UaGreen.Site.Api.Common;

namespace UaGreen.Site.Api.Infrastructure.Captcha;

public sealed class TurnstileOptions
{
    /// Cloudflare Turnstile secret key. Empty → captcha off (dev). The matching site key
    /// goes to Nuxt (NUXT_PUBLIC_TURNSTILE_SITE_KEY): set both or neither.
    public string SecretKey { get; set; } = "";
}

/// Server half of the Cloudflare Turnstile captcha on the public forms.
public sealed class Turnstile(HttpClient http, IOptions<TurnstileOptions> opts, ILogger<Turnstile> log)
{
    public const string VerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    private sealed record VerifyResponse(bool Success, [property: JsonPropertyName("error-codes")] string[]? ErrorCodes);

    public async Task EnsureHumanAsync(string? token, CancellationToken ct)
    {
        var secret = opts.Value.SecretKey;
        if (string.IsNullOrEmpty(secret)) return;
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048) throw Failed();

        VerifyResponse? res;
        try
        {
            using var resp = await http.PostAsync(VerifyUrl, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = secret,
                ["response"] = token,
            }), ct);
            resp.EnsureSuccessStatusCode();
            res = await resp.Content.ReadFromJsonAsync<VerifyResponse>(ct);
        }
        catch (Exception ex) when ((ex is HttpRequestException or JsonException) || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            // ponytail: fail open only when Cloudflare itself is unreachable (a visitor cannot cause
            // that) — the honeypot and the per-IP limit still apply, and a lost order costs more than a spam.
            log.LogWarning(ex, "Turnstile unreachable, letting the submission through");
            return;
        }
        if (res?.Success == true) return;
        // invalid-input-secret here means a misconfigured key, not a bot.
        log.LogWarning("Turnstile rejected a submission: {Codes}", string.Join(",", res?.ErrorCodes ?? []));
        throw Failed();
    }

    private static ProblemException Failed() => ProblemException.BadRequest("Captcha check failed", ErrorCodes.CaptchaFailed);
}
