using Trail.Application.Features.Reports;
using Trail.Domain.Tasks;

namespace Trail.UnitTests.Reports;

public sealed class ComplianceCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 10, 20);
    private static readonly DateOnly Yesterday = Today.AddDays(-1);
    private static readonly Guid SiteA = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid SiteB = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid Fridge = Guid.NewGuid();
    private static readonly Guid Floors = Guid.NewGuid();

    private static readonly Dictionary<Guid, SiteInfo> Sites = new()
    {
        [SiteA] = new(SiteA, "Alpha", Today),
        [SiteB] = new(SiteB, "Bravo", Today),
    };

    private static ComplianceBucket B(
        SiteTaskStatus status, int count = 1, DateOnly? due = null, Guid? site = null, Guid? template = null,
        bool late = false, bool failures = false, bool awaiting = false, string title = "Fridge check") =>
        new(site ?? SiteA, template ?? Fridge, title, due ?? Yesterday, status, late, failures, awaiting, count);

    private static ComplianceRowResponse Total(params ComplianceBucket[] buckets) =>
        ComplianceCalculator.Compute(buckets, ComplianceGrouping.Site, Sites).Total;

    [Fact]
    public void On_time_completions_score_a_full_hundred()
    {
        var total = Total(B(SiteTaskStatus.Completed, 4));
        total.Due.ShouldBe(4);
        total.CompletedOnTime.ShouldBe(4);
        total.Score.ShouldBe(100m);
    }

    [Fact]
    public void Late_completions_are_due_but_do_not_count_as_on_time()
    {
        var total = Total(B(SiteTaskStatus.Completed, 3), B(SiteTaskStatus.Completed, 1, late: true));
        total.Due.ShouldBe(4);
        total.CompletedOnTime.ShouldBe(3);
        total.CompletedLate.ShouldBe(1);
        total.Score.ShouldBe(75m);
    }

    [Fact]
    public void Todays_open_tasks_are_not_counted_against_the_score()
    {
        var total = Total(B(SiteTaskStatus.Completed, 1, due: Today), B(SiteTaskStatus.Open, 5, due: Today));
        total.Due.ShouldBe(1);
        total.Missed.ShouldBe(0);
        total.Score.ShouldBe(100m);
    }

    [Fact]
    public void Open_tasks_from_earlier_days_count_as_due_and_missed()
    {
        var total = Total(B(SiteTaskStatus.Completed, 1), B(SiteTaskStatus.Open, 1, due: Yesterday));
        total.Due.ShouldBe(2);
        total.Missed.ShouldBe(1);
        total.Score.ShouldBe(50m);
    }

    [Fact]
    public void Missed_tasks_count_as_missed_and_due()
    {
        var total = Total(B(SiteTaskStatus.Missed, 2), B(SiteTaskStatus.Completed, 2));
        total.Due.ShouldBe(4);
        total.Missed.ShouldBe(2);
    }

    [Fact]
    public void Cancelled_tasks_are_excluded_entirely()
    {
        var total = Total(B(SiteTaskStatus.Cancelled, 7), B(SiteTaskStatus.Completed, 1));
        total.Due.ShouldBe(1);
        total.Score.ShouldBe(100m);
    }

    [Fact]
    public void Score_is_null_when_nothing_was_due()
    {
        Total().Score.ShouldBeNull();
        Total(B(SiteTaskStatus.Open, 3, due: Today), B(SiteTaskStatus.Cancelled)).Score.ShouldBeNull();
    }

    [Theory]
    [InlineData(1, 3, 33.3)]
    [InlineData(2, 3, 66.7)]
    [InlineData(1, 8, 12.5)]
    [InlineData(0, 5, 0)]
    public void Score_is_rounded_to_one_decimal(int onTime, int due, double expected) =>
        ComplianceCalculator.Score(onTime, due).ShouldBe((decimal)expected);

    [Fact]
    public void Failures_and_awaiting_approval_are_tallied_for_completed_tasks_only()
    {
        var total = Total(
            B(SiteTaskStatus.Completed, 2, failures: true),
            B(SiteTaskStatus.Completed, 3, awaiting: true),
            B(SiteTaskStatus.Missed, 1, failures: true));
        total.WithFailures.ShouldBe(2);
        total.AwaitingApproval.ShouldBe(3);
    }

    [Fact]
    public void Each_site_uses_its_own_local_today()
    {
        var ahead = Guid.NewGuid();
        var sites = new Dictionary<Guid, SiteInfo>
        {
            [SiteA] = new(SiteA, "Alpha", Today),
            [ahead] = new(ahead, "Ahead", Today.AddDays(1)), // already tomorrow there, so "today" for A is past due
        };
        var (rows, total) = ComplianceCalculator.Compute(
            [B(SiteTaskStatus.Open, 1, due: Today), B(SiteTaskStatus.Open, 1, due: Today, site: ahead)],
            ComplianceGrouping.Site, sites);

        total.Due.ShouldBe(1);
        rows.Single().Name.ShouldBe("Ahead");
        rows.Single().Missed.ShouldBe(1);
    }

    [Fact]
    public void Grouping_by_site_orders_by_name_and_sums_to_the_total()
    {
        var (rows, total) = ComplianceCalculator.Compute(
            [B(SiteTaskStatus.Completed, 2, site: SiteB), B(SiteTaskStatus.Completed, 1, site: SiteA), B(SiteTaskStatus.Missed, 1, site: SiteA)],
            ComplianceGrouping.Site, Sites);

        rows.Select(r => r.Name).ShouldBe(["Alpha", "Bravo"]);
        rows.Select(r => r.Key).ShouldBe([SiteA.ToString("D"), SiteB.ToString("D")]);
        rows.Sum(r => r.Due).ShouldBe(total.Due);
        total.Due.ShouldBe(4);
        total.Score.ShouldBe(75m);
    }

    [Fact]
    public void Grouping_by_day_uses_iso_dates_in_order()
    {
        var (rows, _) = ComplianceCalculator.Compute(
            [B(SiteTaskStatus.Completed, due: Yesterday), B(SiteTaskStatus.Completed, due: Yesterday.AddDays(-3)), B(SiteTaskStatus.Completed, due: Yesterday)],
            ComplianceGrouping.Day, Sites);

        rows.Select(r => r.Key).ShouldBe(["2026-10-16", "2026-10-19"]);
        rows[1].Due.ShouldBe(2);
    }

    [Fact]
    public void Grouping_by_template_merges_buckets_and_names_the_template()
    {
        var (rows, _) = ComplianceCalculator.Compute(
            [B(SiteTaskStatus.Completed, template: Fridge, title: "Fridge check"),
             B(SiteTaskStatus.Missed, template: Fridge, title: "Fridge check", site: SiteB),
             B(SiteTaskStatus.Completed, template: Floors, title: "Floors")],
            ComplianceGrouping.Template, Sites);

        rows.Select(r => r.Name).ShouldBe(["Floors", "Fridge check"]);
        rows.Single(r => r.Key == Fridge.ToString("D")).Due.ShouldBe(2);
    }

    [Fact]
    public void Buckets_for_unknown_sites_are_ignored() =>
        Total(B(SiteTaskStatus.Completed, site: Guid.NewGuid())).Due.ShouldBe(0);
}

public sealed class ReportRangeTests
{
    private static readonly DateOnly Today = new(2026, 10, 20);

    [Fact]
    public void Missing_bounds_default_to_thirty_days_ending_today()
    {
        var range = ReportRange.Resolve(null, null, Today).Value;
        range.To.ShouldBe(Today);
        range.From.ShouldBe(Today.AddDays(-30));
    }

    [Fact]
    public void A_missing_start_is_thirty_days_before_the_given_end()
    {
        var range = ReportRange.Resolve(null, new DateOnly(2026, 3, 31), Today).Value;
        range.From.ShouldBe(new DateOnly(2026, 3, 1));
    }

    [Fact]
    public void Ninety_three_days_inclusive_is_allowed_and_ninety_four_is_not()
    {
        var from = new DateOnly(2026, 7, 1);
        ReportRange.Resolve(from, from.AddDays(92), Today).IsSuccess.ShouldBeTrue();

        var result = ReportRange.Resolve(from, from.AddDays(93), Today);
        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("report.invalid_range");
    }

    [Fact]
    public void An_end_before_the_start_is_rejected()
    {
        var result = ReportRange.Resolve(Today, Today.AddDays(-1), Today);
        result.Error!.Code.ShouldBe("report.invalid_range");
    }

    [Fact]
    public void A_missing_end_with_an_old_start_can_exceed_the_cap()
    {
        ReportRange.Resolve(Today.AddDays(-200), null, Today).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Utc_bounds_and_file_suffix_cover_whole_days()
    {
        var range = new ReportDateRange(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 20));
        range.FromUtc.ShouldBe(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        range.ToExclusiveUtc.ShouldBe(new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc));
        range.FileSuffix.ShouldBe("2026-10-01-to-2026-10-20");
    }
}
