using Trail.Domain.Common;
using Trail.Domain.Tasks;

namespace Trail.UnitTests.Tasks;

public sealed class SiteTimeAndTaskTests
{
    private static readonly TimeZoneInfo London = SiteTime.Zone("Europe/London");

    [Fact]
    public void Local_times_convert_with_the_seasonal_offset()
    {
        SiteTime.ToUtc(new DateOnly(2026, 1, 15), new TimeOnly(9, 0), London).ShouldBe(new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc));
        SiteTime.ToUtc(new DateOnly(2026, 7, 15), new TimeOnly(9, 0), London).ShouldBe(new DateTime(2026, 7, 15, 8, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void A_time_skipped_by_spring_forward_moves_past_the_gap()
    {
        // Clocks go from 01:00 to 02:00 on 29 March 2026 in London; 01:30 does not exist.
        var utc = SiteTime.ToUtc(new DateOnly(2026, 3, 29), new TimeOnly(1, 30), London);

        utc.ShouldBe(new DateTime(2026, 3, 29, 1, 30, 0, DateTimeKind.Utc)); // 02:30 BST
    }

    [Fact]
    public void A_time_repeated_by_fall_back_resolves_to_the_first_occurrence()
    {
        // 01:30 happens twice on 25 October 2026; the first is 00:30 UTC (BST).
        SiteTime.ToUtc(new DateOnly(2026, 10, 25), new TimeOnly(1, 30), London)
            .ShouldBe(new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Today_is_the_sites_local_date()
    {
        var auckland = SiteTime.Zone("Pacific/Auckland");

        SiteTime.Today(new DateTime(2026, 6, 1, 13, 0, 0, DateTimeKind.Utc), auckland).ShouldBe(new DateOnly(2026, 6, 2));
        SiteTime.Today(new DateTime(2026, 6, 1, 13, 0, 0, DateTimeKind.Utc), London).ShouldBe(new DateOnly(2026, 6, 1));
    }

    private static SiteTask NewTask(bool requiresApproval = false) => new()
    {
        Title = "Opening checks",
        ScheduledDate = new DateOnly(2026, 1, 15),
        DueDate = new DateOnly(2026, 1, 15),
        DueAt = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
        AvailableAt = new DateTime(2026, 1, 15, 6, 0, 0, DateTimeKind.Utc),
        RequiresApproval = requiresApproval,
    };

    [Fact]
    public void Completing_after_the_due_time_marks_the_task_late()
    {
        var onTime = NewTask();
        onTime.Complete(new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc), Guid.NewGuid(), hasFailures: false);
        onTime.CompletedLate.ShouldBeFalse();

        var late = NewTask();
        late.IsOverdue(new DateTime(2026, 1, 15, 11, 0, 0, DateTimeKind.Utc)).ShouldBeTrue();
        late.Complete(new DateTime(2026, 1, 15, 11, 0, 0, DateTimeKind.Utc), Guid.NewGuid(), hasFailures: true);
        late.CompletedLate.ShouldBeTrue();
        late.HasFailures.ShouldBeTrue();
    }

    [Fact]
    public void Missed_tasks_can_be_completed_late_only_within_the_window()
    {
        var task = NewTask();
        task.Status = SiteTaskStatus.Missed;

        task.CanBeWorkedOn(task.DueAt.AddDays(6)).ShouldBeTrue();
        task.CanBeWorkedOn(task.DueAt.AddDays(8)).ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => task.Complete(task.DueAt.AddDays(8), null, false));
    }

    [Fact]
    public void Approval_is_only_for_completed_tasks_that_need_it()
    {
        var task = NewTask(requiresApproval: true);
        Should.Throw<InvalidOperationException>(() => task.Approve(DateTime.UtcNow, null, null));

        task.Complete(task.DueAt.AddHours(-1), Guid.NewGuid(), false);
        task.IsAwaitingApproval.ShouldBeTrue();
        task.Approve(task.DueAt, Guid.NewGuid(), "Looks good");
        task.IsAwaitingApproval.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => task.Reopen());
    }

    [Fact]
    public void Snoozing_moves_the_day_and_keeps_the_local_due_time_across_a_clock_change()
    {
        // Due 10:00 local on 27 March (GMT, 10:00 UTC); snoozed past the 29 March clock change.
        var task = NewTask();
        task.DueDate = new DateOnly(2026, 3, 27);
        task.DueAt = new DateTime(2026, 3, 27, 10, 0, 0, DateTimeKind.Utc);
        task.AvailableAt = null;

        task.Snooze(new DateOnly(2026, 3, 30), London);

        task.DueDate.ShouldBe(new DateOnly(2026, 3, 30));
        task.DueAt.ShouldBe(new DateTime(2026, 3, 30, 9, 0, 0, DateTimeKind.Utc)); // 10:00 BST
        task.ScheduledDate.ShouldBe(new DateOnly(2026, 1, 15));
        task.SnoozeCount.ShouldBe(1);
        Should.Throw<InvalidOperationException>(() => task.Snooze(new DateOnly(2026, 3, 30), London));
    }
}
