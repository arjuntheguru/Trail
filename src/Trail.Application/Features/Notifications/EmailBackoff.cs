using Trail.Domain.Notifications;

namespace Trail.Application.Features.Notifications;

/// <summary>When a failed notification email may be tried again. Pure.</summary>
public static class EmailBackoff
{
    private static readonly TimeSpan[] Delays =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(60)];

    public static TimeSpan MinimumDelay => Delays[0];

    /// <summary>The wait before the next attempt after <paramref name="failedAttempts"/> failures (0: none).</summary>
    public static TimeSpan DelayAfter(int failedAttempts) =>
        failedAttempts <= 0 ? TimeSpan.Zero : Delays[Math.Min(failedAttempts, Delays.Length) - 1];

    /// <param name="lastTouchedAt">The row's UpdatedAt, which the failed attempt stamped.</param>
    public static bool IsDue(int failedAttempts, DateTime? lastTouchedAt, DateTime now) =>
        failedAttempts <= 0 || lastTouchedAt is null || now >= lastTouchedAt.Value + DelayAfter(failedAttempts);

    public static bool ShouldGiveUp(int failedAttempts) => failedAttempts >= Notification.MaxEmailAttempts;
}
