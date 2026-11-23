using UaGreen.Site.Api.Features.Audit;

namespace UaGreen.Site.Tests;

public class AuditWindowTests
{
    [Fact]
    public void AuditWindow_uses_kyiv_date()
    {
        // 22:30 UTC on 30 Nov is already 1 Dec in Kyiv (UTC+2): the window is 1 Dec + 7 … 1 Dec + 30.
        var (from, to) = AuditWindow.For(new DateTime(2026, 11, 30, 22, 30, 0, DateTimeKind.Utc));

        Assert.Equal(new DateOnly(2026, 12, 8), from);
        Assert.Equal(new DateOnly(2026, 12, 31), to);
    }

    [Fact]
    public void AuditWindow_rejects_dates_outside_window_or_not_free()
    {
        var now = new DateTime(2026, 11, 30, 22, 30, 0, DateTimeKind.Utc);
        // The window filter must hold even if Gospodar listed a day outside it.
        DateOnly[] free = [new(2026, 12, 7), new(2026, 12, 8), new(2026, 12, 10), new(2027, 1, 1)];

        Assert.True(AuditWindow.IsBookable(new DateOnly(2026, 12, 8), now, free));
        Assert.False(AuditWindow.IsBookable(new DateOnly(2026, 12, 9), now, free));  // not free
        Assert.False(AuditWindow.IsBookable(new DateOnly(2026, 12, 7), now, free));  // too early
        Assert.False(AuditWindow.IsBookable(new DateOnly(2027, 1, 1), now, free));   // too late
    }
}
