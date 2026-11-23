using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Infrastructure.Captcha;

namespace UaGreen.Site.Tests;

public class TurnstileTests
{
    private sealed class Stub(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(respond());
        }
    }

    private static (Turnstile, Stub) Make(string secret, Func<HttpResponseMessage> respond)
    {
        var stub = new Stub(respond);
        return (new Turnstile(new HttpClient(stub), Options.Create(new TurnstileOptions { SecretKey = secret }),
            NullLogger<Turnstile>.Instance), stub);
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    [Fact]
    public async Task Passes_when_cloudflare_says_success()
    {
        var (t, stub) = Make("s", () => Json("""{"success":true,"error-codes":[]}"""));
        await t.EnsureHumanAsync("tok", default);
        Assert.Equal(1, stub.Calls);
    }

    [Fact]
    public async Task Rejects_bad_or_missing_token()
    {
        var (t, stub) = Make("s", () => Json("""{"success":false,"error-codes":["invalid-input-response"]}"""));
        var ex = await Assert.ThrowsAsync<ProblemException>(() => t.EnsureHumanAsync("tok", default));
        Assert.Equal(ErrorCodes.CaptchaFailed, ex.Code);
        await Assert.ThrowsAsync<ProblemException>(() => t.EnsureHumanAsync(null, default));
        Assert.Equal(1, stub.Calls); // an empty token never reaches Cloudflare
    }

    [Fact]
    public async Task Fails_open_when_cloudflare_is_down()
    {
        var (t, _) = Make("s", () => new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("<html>") });
        await t.EnsureHumanAsync("tok", default);
    }

    [Fact]
    public async Task Disabled_without_secret()
    {
        var (t, stub) = Make("", () => throw new InvalidOperationException("must not call"));
        await t.EnsureHumanAsync(null, default);
        Assert.Equal(0, stub.Calls);
    }
}
