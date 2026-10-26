using Microsoft.Extensions.Options;

namespace UaGreen.Site.Api.Infrastructure.Auth;

/// Gospodar bumps a "session epoch" on each of its releases; tokens issued before it
/// are rejected. Copied from TTApp: cache + policy + background refresher.
public sealed class SessionEpochCache
{
    private long _ticks = DateTime.MinValue.Ticks;
    public DateTime ValidFromUtc => new(Interlocked.Read(ref _ticks), DateTimeKind.Utc);
    public void Set(DateTime validFromUtc) => Interlocked.Exchange(ref _ticks, validFromUtc.Ticks);
}

public static class SessionEpochPolicy
{
    public static readonly TimeSpan SkewTolerance = TimeSpan.FromSeconds(2);

    public static bool IsInvalidated(DateTime issuedAtUtc, DateTime cutoffUtc)
    {
        if (cutoffUtc == DateTime.MinValue) return false; // no cutoff known yet — fail open
        return issuedAtUtc < cutoffUtc - SkewTolerance;
    }
}

public sealed class SessionEpochRefresher(
    IServiceScopeFactory scopeFactory,
    SessionEpochCache cache,
    IOptions<GospodarOptions> options,
    ILogger<SessionEpochRefresher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, options.Value.SessionEpochPollSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var client = scope.ServiceProvider.GetRequiredService<GospodarClient>();
                var epoch = await client.GetSessionEpochAsync(stoppingToken);
                if (epoch.HasValue) cache.Set(epoch.Value);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogDebug(ex, "Session-epoch refresh failed; keeping last-known cutoff.");
            }
            try { await Task.Delay(interval, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
