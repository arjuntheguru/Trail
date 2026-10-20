using Trail.Domain.Integrations;

namespace Trail.Application.Features.Integrations;

/// <summary>When a failed delivery is tried again, and when it is given up on.</summary>
public static class WebhookRetryPolicy
{
    /// <summary>Wait after attempt 1, 2, ... 6. Later attempts reuse the last value.</summary>
    private static readonly TimeSpan[] Backoff =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2), TimeSpan.FromHours(6), TimeSpan.FromHours(24)];

    /// <summary>How long a delivery may stay Sending before another instance takes it over.</summary>
    public static readonly TimeSpan StuckAfter = TimeSpan.FromMinutes(5);

    /// <param name="attemptsMade">Attempts so far, including the one that just failed.</param>
    /// <returns>The delay before the next attempt, or null when no attempts remain.</returns>
    public static TimeSpan? DelayAfter(int attemptsMade) =>
        attemptsMade >= WebhookDelivery.MaxAttempts ? null : Backoff[Math.Clamp(attemptsMade, 1, Backoff.Length) - 1];

    public static (WebhookDeliveryStatus Status, DateTime NextAttemptAt) OnFailure(int attemptsMade, DateTime now) =>
        DelayAfter(attemptsMade) is { } delay
            ? (WebhookDeliveryStatus.Pending, now + delay)
            : (WebhookDeliveryStatus.Failed, now);

    public static bool IsSuccess(int statusCode) => statusCode is >= 200 and < 300;
}
