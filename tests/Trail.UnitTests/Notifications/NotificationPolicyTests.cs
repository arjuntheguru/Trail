using Trail.Application.Features.Notifications;
using Trail.Domain.Issues;
using Trail.Domain.Notifications;

namespace Trail.UnitTests.Notifications;

public sealed class NotificationPolicyTests
{
    private static readonly Guid A = Guid.CreateVersion7();
    private static readonly Guid B = Guid.CreateVersion7();
    private static readonly Guid C = Guid.CreateVersion7();

    [Theory]
    [InlineData(IssuePriority.Low, IssueSource.Manual, false, true)]
    [InlineData(IssuePriority.Medium, IssueSource.Manual, false, true)]
    [InlineData(IssuePriority.Low, IssueSource.Manual, true, false)]
    [InlineData(IssuePriority.Medium, IssueSource.Manual, true, false)]
    [InlineData(IssuePriority.High, IssueSource.Manual, true, true)]
    [InlineData(IssuePriority.Critical, IssueSource.Manual, true, true)]
    [InlineData(IssuePriority.Medium, IssueSource.TaskFailure, true, true)]
    public void Managers_hear_of_a_new_issue_per_priority_source_and_assignee(
        IssuePriority priority, IssueSource source, bool hasAssignee, bool expected) =>
        NotificationPolicy.NotifyManagersOfRaise(priority, source, hasAssignee).ShouldBe(expected);

    [Theory]
    [InlineData(IssueStatus.InProgress, IssueStatus.Resolved, StatusChangeAudience.Managers)]
    [InlineData(IssueStatus.Resolved, IssueStatus.Closed, StatusChangeAudience.Assignee)]
    [InlineData(IssueStatus.Open, IssueStatus.Closed, StatusChangeAudience.Assignee)]
    [InlineData(IssueStatus.Resolved, IssueStatus.Open, StatusChangeAudience.Assignee)]
    [InlineData(IssueStatus.Closed, IssueStatus.Open, StatusChangeAudience.Assignee)]
    [InlineData(IssueStatus.Open, IssueStatus.InProgress, StatusChangeAudience.None)]
    public void Status_changes_reach_the_right_audience(IssueStatus from, IssueStatus to, StatusChangeAudience expected) =>
        NotificationPolicy.AudienceForStatusChange(from, to).ShouldBe(expected);

    [Fact]
    public void Status_change_verbs_distinguish_reopening()
    {
        NotificationPolicy.StatusChangeVerb(IssueStatus.Closed, IssueStatus.Open).ShouldBe("reopened");
        NotificationPolicy.StatusChangeVerb(IssueStatus.Open, IssueStatus.InProgress).ShouldBe("started");
    }

    [Fact]
    public void Comments_go_to_the_assignee_or_else_the_raiser()
    {
        NotificationPolicy.CommentRecipient(A, B).ShouldBe(A);
        NotificationPolicy.CommentRecipient(null, B).ShouldBe(B);
        NotificationPolicy.CommentRecipient(null, null).ShouldBeNull();
    }

    [Fact]
    public void Recipients_are_distinct_and_exclude_the_actor_and_anyone_covered_elsewhere()
    {
        NotificationPolicy.Recipients([A, B, B, C], excluded: [A, null]).ShouldBe([B, C]);
        NotificationPolicy.Recipients([A], excluded: [A]).ShouldBeEmpty();
    }

    [Fact]
    public void Preferences_default_to_the_actionable_kinds()
    {
        foreach (var kind in new[] { NotificationKind.IssueAssigned, NotificationKind.IssueEscalated, NotificationKind.IssueOverdue, NotificationKind.TaskAwaitingApproval })
            NotificationPreference.WantsEmail(null, kind).ShouldBeTrue();
        foreach (var kind in new[] { NotificationKind.IssueRaised, NotificationKind.IssueCommented, NotificationKind.IssueStatusChanged })
            NotificationPreference.WantsEmail(null, kind).ShouldBeFalse();
    }

    [Fact]
    public void A_saved_preference_overrides_the_defaults()
    {
        var preference = new NotificationPreference { MembershipId = A, EmailKinds = [NotificationKind.IssueCommented] };

        NotificationPreference.WantsEmail(preference, NotificationKind.IssueCommented).ShouldBeTrue();
        NotificationPreference.WantsEmail(preference, NotificationKind.IssueAssigned).ShouldBeFalse();
    }

    [Fact]
    public void Texts_fit_the_column_limits()
    {
        var (title, body) = NotificationText.IssueCommented(7, new string('x', 400), "Site", new string('y', 5000));

        title.ShouldContain("#7");
        body.Length.ShouldBeLessThanOrEqualTo(Notification.BodyMaxLength);
    }
}
