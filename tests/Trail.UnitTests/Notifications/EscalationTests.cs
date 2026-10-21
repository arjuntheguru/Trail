using Trail.Application.Features.Notifications;
using Trail.Domain.Issues;

namespace Trail.UnitTests.Notifications;

public sealed class EscalationTests
{
    private static readonly DateTime Due = new(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);

    private static EscalationDecision Decide(
        DateTime now, IssuePriority priority = IssuePriority.High, IssueStatus status = IssueStatus.Open,
        DateTime? notified = null, DateTime? escalated = null, DateTime? due = null) =>
        EscalationPolicy.Decide(status, priority, due ?? Due, notified, escalated, now);

    [Theory]
    [InlineData(IssuePriority.Critical, 4)]
    [InlineData(IssuePriority.High, 24)]
    [InlineData(IssuePriority.Medium, 72)]
    [InlineData(IssuePriority.Low, 168)]
    public void The_escalation_cutoff_matches_when_Decide_starts_escalating(IssuePriority priority, int hours)
    {
        var now = Due.AddDays(30);
        var cutoff = EscalationPolicy.EscalationCutoff(priority, now);

        cutoff.ShouldBe(now.AddHours(-hours));
        Decide(now, priority).Escalate.ShouldBeTrue();
        EscalationPolicy.Decide(IssueStatus.Open, priority, cutoff, null, null, now).Escalate.ShouldBeFalse();
        EscalationPolicy.Decide(IssueStatus.Open, priority, cutoff.AddTicks(-1), null, null, now).Escalate.ShouldBeTrue();
    }

    [Fact]
    public void Nothing_happens_before_the_due_time_or_without_one()
    {
        Decide(Due).ShouldBe(EscalationDecision.Nothing);
        Decide(Due.AddMinutes(-1)).ShouldBe(EscalationDecision.Nothing);
        EscalationPolicy.Decide(IssueStatus.Open, IssuePriority.High, null, null, null, Due.AddDays(9)).ShouldBe(EscalationDecision.Nothing);
    }

    [Theory]
    [InlineData(IssueStatus.Resolved)]
    [InlineData(IssueStatus.Closed)]
    public void Finished_issues_are_left_alone(IssueStatus status) =>
        Decide(Due.AddDays(30), status: status).ShouldBe(EscalationDecision.Nothing);

    [Fact]
    public void A_newly_overdue_issue_is_announced_but_not_yet_escalated()
    {
        Decide(Due.AddMinutes(5)).ShouldBe(new EscalationDecision(NotifyOverdue: true, Escalate: false));
    }

    [Fact]
    public void The_overdue_notice_is_sent_once()
    {
        Decide(Due.AddMinutes(10), notified: Due.AddMinutes(5)).ShouldBe(EscalationDecision.Nothing);
    }

    [Theory]
    [InlineData(IssuePriority.Critical, 4)]
    [InlineData(IssuePriority.High, 24)]
    [InlineData(IssuePriority.Medium, 72)]
    [InlineData(IssuePriority.Low, 168)]
    public void Escalation_waits_one_default_response_time_past_the_due_time(IssuePriority priority, int hours)
    {
        var notified = Due.AddMinutes(1);

        Decide(Due.AddHours(hours), priority, notified: notified).Escalate.ShouldBeFalse();
        Decide(Due.AddHours(hours).AddMinutes(1), priority, notified: notified)
            .ShouldBe(new EscalationDecision(NotifyOverdue: false, Escalate: true));
    }

    [Fact]
    public void An_issue_found_very_late_gets_both_the_notice_and_the_escalation()
    {
        Decide(Due.AddDays(5)).ShouldBe(new EscalationDecision(NotifyOverdue: true, Escalate: true));
    }

    [Fact]
    public void Escalation_happens_once()
    {
        Decide(Due.AddDays(5), notified: Due.AddHours(1), escalated: Due.AddDays(2)).ShouldBe(EscalationDecision.Nothing);
    }

    [Fact]
    public void Overdue_notices_go_to_the_assignee_else_all_managers()
    {
        var assignee = Guid.CreateVersion7();
        var m1 = new SiteMember(Guid.CreateVersion7(), Guid.CreateVersion7(), ReachLevel.Site);
        var m2 = new SiteMember(Guid.CreateVersion7(), Guid.CreateVersion7(), ReachLevel.AllSites);

        EscalationPolicy.OverdueRecipients(assignee, [m1, m2]).ShouldBe([assignee]);
        EscalationPolicy.OverdueRecipients(null, [m1, m2]).ShouldBe([m1.MembershipId, m2.MembershipId]);
    }

    [Fact]
    public void Escalations_go_to_region_and_organization_managers_before_site_managers()
    {
        var site = new SiteMember(Guid.CreateVersion7(), Guid.CreateVersion7(), ReachLevel.Site);
        var region = new SiteMember(Guid.CreateVersion7(), Guid.CreateVersion7(), ReachLevel.Region);
        var all = new SiteMember(Guid.CreateVersion7(), Guid.CreateVersion7(), ReachLevel.AllSites);

        EscalationPolicy.EscalationRecipients([site, region, all]).ShouldBe([region.MembershipId, all.MembershipId]);
    }

    [Fact]
    public void Escalations_fall_back_to_all_site_managers_when_nobody_is_above_them()
    {
        var one = new SiteMember(Guid.CreateVersion7(), Guid.CreateVersion7(), ReachLevel.Site);
        var two = new SiteMember(Guid.CreateVersion7(), Guid.CreateVersion7(), ReachLevel.Site);

        EscalationPolicy.EscalationRecipients([one, two]).ShouldBe([one.MembershipId, two.MembershipId]);
        EscalationPolicy.EscalationRecipients([]).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true, false, ReachLevel.AllSites)]
    [InlineData(true, true, ReachLevel.AllSites)]
    [InlineData(false, true, ReachLevel.Region)]
    [InlineData(false, false, ReachLevel.Site)]
    public void Reach_level_follows_the_widest_access(bool allSites, bool viaRegion, ReachLevel expected) =>
        EscalationPolicy.LevelOf(allSites, viaRegion).ShouldBe(expected);

    [Fact]
    public void MarkEscalated_stamps_once_and_a_new_due_time_clears_the_escalation()
    {
        var issue = new Issue { Title = "Fridge warm", SiteId = Guid.CreateVersion7() };
        issue.Raised(Due.AddDays(-1), null, Due);

        issue.MarkOverdueNotified(Due.AddHours(1));
        issue.MarkEscalated(Due.AddHours(30));
        issue.MarkEscalated(Due.AddHours(40));

        issue.OverdueNotifiedAt.ShouldBe(Due.AddHours(1));
        issue.EscalatedAt.ShouldBe(Due.AddHours(30));

        issue.SetDue(Due.AddHours(50), null, Due.AddDays(7));

        issue.OverdueNotifiedAt.ShouldBeNull();
        issue.EscalatedAt.ShouldBeNull();
    }
}
