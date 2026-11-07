using NSubstitute;
using Trail.Application.Features.Notifications;
using Trail.Domain.Notifications;

namespace Trail.UnitTests.Notifications;

public sealed class EmailTests
{
    private static readonly DateTime Now = new(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 15)]
    [InlineData(4, 60)]
    [InlineData(9, 60)]
    public void Backoff_grows_with_each_failure(int failures, int minutes) =>
        EmailBackoff.DelayAfter(failures).ShouldBe(TimeSpan.FromMinutes(minutes));

    [Fact]
    public void A_fresh_notification_is_always_due()
    {
        EmailBackoff.IsDue(0, null, Now).ShouldBeTrue();
        EmailBackoff.IsDue(0, Now, Now).ShouldBeTrue();
    }

    [Fact]
    public void A_retry_is_due_only_after_its_delay_has_passed_since_the_last_touch()
    {
        EmailBackoff.IsDue(2, Now.AddMinutes(-4), Now).ShouldBeFalse();
        EmailBackoff.IsDue(2, Now.AddMinutes(-5), Now).ShouldBeTrue();
        EmailBackoff.IsDue(4, Now.AddMinutes(-59), Now).ShouldBeFalse();
        EmailBackoff.IsDue(4, Now.AddMinutes(-60), Now).ShouldBeTrue();
    }

    [Fact]
    public void The_fifth_failure_ends_the_retries()
    {
        EmailBackoff.ShouldGiveUp(4).ShouldBeFalse();
        EmailBackoff.ShouldGiveUp(Notification.MaxEmailAttempts).ShouldBeTrue();
    }

    [Fact]
    public void An_issue_notification_email_links_to_the_issue()
    {
        var links = Substitute.For<INotificationLinks>();
        var issueId = Guid.CreateVersion7();
        links.Issue(issueId).Returns("https://app/issues/1");
        links.NotificationSettings().Returns("https://app/settings");
        var notification = new Notification
        {
            Title = "Issue #4 was assigned to you",
            Body = "Fridge warm at Soho",
            Kind = NotificationKind.IssueAssigned,
            SubjectType = NotificationSubjectType.Issue,
            SubjectId = issueId,
        };

        var message = NotificationEmailComposer.Compose(notification, "sam@example.com", "Sam", links);

        message.To.ShouldBe("sam@example.com");
        message.Subject.ShouldBe("Issue #4 was assigned to you");
        message.TextBody.ShouldContain("Hi Sam");
        message.TextBody.ShouldContain("Fridge warm at Soho");
        message.TextBody.ShouldContain("https://app/issues/1");
    }

    [Fact]
    public void A_task_notification_email_links_to_the_task()
    {
        var links = Substitute.For<INotificationLinks>();
        var taskId = Guid.CreateVersion7();
        links.Task(taskId).Returns("https://app/tasks/9");
        var notification = new Notification
        {
            Title = "Waiting for approval",
            Body = "Opening checks",
            SubjectType = NotificationSubjectType.Task,
            SubjectId = taskId,
        };

        NotificationEmailComposer.Compose(notification, "a@b.c", "Al", links).TextBody.ShouldContain("https://app/tasks/9");
    }
}
