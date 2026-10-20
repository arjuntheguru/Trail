using Trail.Application.Features.Reports;
using Trail.Domain.Issues;
using Trail.Domain.Tasks;

namespace Trail.UnitTests.Reports;

public sealed class DashboardCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 10, 20);
    private static readonly Guid SiteA = Guid.NewGuid();
    private static readonly Guid SiteB = Guid.NewGuid();
    private static readonly SiteInfo A = new(SiteA, "Alpha", Today);
    private static readonly SiteInfo B = new(SiteB, "Bravo", Today.AddDays(1));

    private static DashboardTaskBucket T(
        Guid site, DateOnly due, SiteTaskStatus status, int count = 1, bool late = false, bool overdue = false) =>
        new(site, due, status, late, overdue, count);

    [Fact]
    public void Counts_todays_tasks_by_outcome()
    {
        var result = DashboardCalculator.Compute(
            [A],
            [T(SiteA, Today, SiteTaskStatus.Completed, 3),
             T(SiteA, Today, SiteTaskStatus.Completed, 1, late: true),
             T(SiteA, Today, SiteTaskStatus.Open, 4),
             T(SiteA, Today, SiteTaskStatus.Open, 2, overdue: true)],
            []);

        var site = result.Sites.Single();
        site.DueToday.ShouldBe(10);
        site.Completed.ShouldBe(4);
        site.CompletedOnTime.ShouldBe(3);
        site.Remaining.ShouldBe(6);
        site.Overdue.ShouldBe(2);
        site.Score.ShouldBe(30m);
    }

    [Fact]
    public void Missed_yesterday_counts_missed_and_still_open_tasks_but_not_completed_ones()
    {
        var yesterday = Today.AddDays(-1);
        var result = DashboardCalculator.Compute(
            [A],
            [T(SiteA, yesterday, SiteTaskStatus.Missed, 2),
             T(SiteA, yesterday, SiteTaskStatus.Open, 1, overdue: true),
             T(SiteA, yesterday, SiteTaskStatus.Completed, 5),
             T(SiteA, yesterday, SiteTaskStatus.Cancelled, 9)],
            []);

        var site = result.Sites.Single();
        site.MissedYesterday.ShouldBe(3);
        site.DueToday.ShouldBe(0);
        site.Score.ShouldBeNull();
    }

    [Fact]
    public void Each_site_is_measured_against_its_own_today()
    {
        var result = DashboardCalculator.Compute(
            [A, B],
            [T(SiteA, Today, SiteTaskStatus.Completed), T(SiteB, Today, SiteTaskStatus.Missed), T(SiteB, Today.AddDays(1), SiteTaskStatus.Open)],
            []);

        result.Sites.Single(s => s.SiteId == SiteA).DueToday.ShouldBe(1);
        var b = result.Sites.Single(s => s.SiteId == SiteB);
        b.DueToday.ShouldBe(1);
        b.Remaining.ShouldBe(1);
        b.MissedYesterday.ShouldBe(1);
        b.Today.ShouldBe(Today.AddDays(1));
    }

    [Fact]
    public void Issue_counts_include_all_four_priorities_and_totals_sum_sites()
    {
        var result = DashboardCalculator.Compute(
            [A, B],
            [T(SiteA, Today, SiteTaskStatus.Completed, 2), T(SiteB, Today.AddDays(1), SiteTaskStatus.Open, 2)],
            [new(SiteA, IssuePriority.High, false, 2), new(SiteA, IssuePriority.High, true, 1), new(SiteB, IssuePriority.Low, false, 4)]);

        var a = result.Sites.Single(s => s.SiteId == SiteA);
        a.OpenIssues.ShouldBe(3);
        a.OverdueIssues.ShouldBe(1);
        a.OpenByPriority.Keys.ShouldBe(Enum.GetValues<IssuePriority>(), ignoreOrder: true);
        a.OpenByPriority[IssuePriority.High].ShouldBe(3);
        a.OpenByPriority[IssuePriority.Critical].ShouldBe(0);

        result.Totals.OpenIssues.ShouldBe(7);
        result.Totals.OpenByPriority[IssuePriority.Low].ShouldBe(4);
        result.Totals.DueToday.ShouldBe(4);
        result.Totals.Score.ShouldBe(50m);
    }

    [Fact]
    public void A_site_with_no_data_still_appears_with_zeroes()
    {
        var result = DashboardCalculator.Compute([A], [], []);
        var site = result.Sites.Single();
        site.DueToday.ShouldBe(0);
        site.Score.ShouldBeNull();
        site.OpenByPriority.Count.ShouldBe(4);
        result.Totals.Score.ShouldBeNull();
    }

    [Fact]
    public void Sites_are_ordered_by_name()
    {
        var result = DashboardCalculator.Compute([B, A], [], []);
        result.Sites.Select(s => s.SiteName).ShouldBe(["Alpha", "Bravo"]);
    }
}

public sealed class ActivityMergerTests
{
    private static readonly DateTime Base = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    private static ActivityRow R(int minutesAgo, string kind = ActivityKinds.TaskCompleted) =>
        new(kind, Base.AddMinutes(-minutesAgo), Guid.Empty, null, Guid.NewGuid(), "x", null);

    [Fact]
    public void Merges_sources_newest_first()
    {
        var (items, next) = ActivityMerger.Merge([R(5), R(1, ActivityKinds.IssueRaised), R(3, ActivityKinds.TaskApproved)], 10);
        items.Select(i => i.OccurredAt).ShouldBe([Base.AddMinutes(-1), Base.AddMinutes(-3), Base.AddMinutes(-5)]);
        next.ShouldBeNull();
    }

    [Fact]
    public void Returns_next_before_when_more_rows_exist()
    {
        var (items, next) = ActivityMerger.Merge([R(1), R(2), R(3)], 2);
        items.Count.ShouldBe(2);
        next.ShouldBe(Base.AddMinutes(-2));
    }

    [Fact]
    public void No_next_before_when_the_page_is_exactly_full()
    {
        var (items, next) = ActivityMerger.Merge([R(1), R(2)], 2);
        items.Count.ShouldBe(2);
        next.ShouldBeNull();
    }

    [Theory]
    [InlineData(null, 50)]
    [InlineData(0, 1)]
    [InlineData(-4, 1)]
    [InlineData(20, 20)]
    [InlineData(1000, 200)]
    public void Limit_defaults_to_fifty_and_is_capped_at_two_hundred(int? requested, int expected) =>
        ActivityMerger.ClampLimit(requested).ShouldBe(expected);

    [Fact]
    public void Issue_events_map_to_feed_kinds_and_noisy_ones_are_left_out()
    {
        ActivityKinds.FromIssueEvent(IssueEventKind.Raised).ShouldBe("issue_raised");
        ActivityKinds.FromIssueEvent(IssueEventKind.StatusChanged).ShouldBe("issue_status_changed");
        ActivityKinds.FromIssueEvent(IssueEventKind.Assigned).ShouldBe("issue_assigned");
        ActivityKinds.FromIssueEvent(IssueEventKind.Comment).ShouldBe("issue_commented");
        ActivityKinds.FromIssueEvent(IssueEventKind.PriorityChanged).ShouldBeNull();
        ActivityKinds.FromIssueEvent(IssueEventKind.DueChanged).ShouldBeNull();
    }
}
