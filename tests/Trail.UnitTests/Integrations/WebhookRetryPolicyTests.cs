using Trail.Application.Features.Integrations;
using Trail.Domain.Integrations;

namespace Trail.UnitTests.Integrations;

public sealed class WebhookRetryPolicyTests
{
    private static readonly DateTime Now = new(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 30)]
    [InlineData(4, 120)]
    [InlineData(5, 360)]
    [InlineData(6, 1440)]
    [InlineData(7, 1440)]
    public void Backoff_follows_the_schedule(int attemptsMade, int minutes) =>
        WebhookRetryPolicy.DelayAfter(attemptsMade).ShouldBe(TimeSpan.FromMinutes(minutes));

    [Fact]
    public void No_delay_after_the_last_attempt() =>
        WebhookRetryPolicy.DelayAfter(WebhookDelivery.MaxAttempts).ShouldBeNull();

    [Fact]
    public void A_failure_before_the_limit_goes_back_to_pending_at_the_backoff_time()
    {
        var (status, next) = WebhookRetryPolicy.OnFailure(3, Now);

        status.ShouldBe(WebhookDeliveryStatus.Pending);
        next.ShouldBe(Now.AddMinutes(30));
    }

    [Fact]
    public void The_eighth_failure_is_final()
    {
        WebhookRetryPolicy.OnFailure(7, Now).Status.ShouldBe(WebhookDeliveryStatus.Pending);
        WebhookRetryPolicy.OnFailure(8, Now).Status.ShouldBe(WebhookDeliveryStatus.Failed);
        WebhookRetryPolicy.OnFailure(9, Now).Status.ShouldBe(WebhookDeliveryStatus.Failed);
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(204, true)]
    [InlineData(299, true)]
    [InlineData(199, false)]
    [InlineData(301, false)]
    [InlineData(404, false)]
    [InlineData(500, false)]
    public void Only_2xx_is_success(int status, bool success) =>
        WebhookRetryPolicy.IsSuccess(status).ShouldBe(success);

    [Fact]
    public void Stuck_deliveries_are_recovered_after_five_minutes() =>
        WebhookRetryPolicy.StuckAfter.ShouldBe(TimeSpan.FromMinutes(5));

    [Fact]
    public void Subscriptions_are_switched_off_after_fifty_consecutive_failures() =>
        WebhookSubscription.AutoDisableAfterFailures.ShouldBe(50);
}
