using Trail.Domain.Issues;

namespace Trail.UnitTests.Issues;

public sealed class IssueTests
{
    private static readonly DateTime Now = new(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Alice = Guid.CreateVersion7();
    private static readonly Guid Bob = Guid.CreateVersion7();

    private static Issue Raise(IssuePriority priority = IssuePriority.Medium)
    {
        var issue = new Issue { Title = "Walk-in fridge at 9°C", SiteId = Guid.CreateVersion7(), Priority = priority };
        issue.Raised(Now, Alice, Now.AddDays(1));
        return issue;
    }

    [Fact]
    public void Raising_records_the_first_event_and_due_time()
    {
        var issue = Raise(IssuePriority.High);

        issue.Status.ShouldBe(IssueStatus.Open);
        issue.DueAt.ShouldBe(Now.AddDays(1));
        var raised = issue.Events.ShouldHaveSingleItem();
        raised.Kind.ShouldBe(IssueEventKind.Raised);
        raised.To.ShouldBe("High");
        raised.ActorId.ShouldBe(Alice);
        raised.IssueId.ShouldBe(issue.Id);
        Should.Throw<InvalidOperationException>(() => issue.Raised(Now, Alice, null));
    }

    [Fact]
    public void The_full_lifecycle_is_recorded_in_order()
    {
        var issue = Raise();

        issue.Assign(Now.AddMinutes(1), Alice, Bob);
        issue.Start(Now.AddMinutes(2), Bob);
        issue.Resolve(Now.AddMinutes(30), Bob, "Moved stock to the spare fridge; engineer called.", []);
        issue.Close(Now.AddHours(2), Alice, "Checked, fridge back at 3°C.");

        issue.Status.ShouldBe(IssueStatus.Closed);
        issue.AssigneeId.ShouldBe(Bob);
        issue.Resolution.ShouldBe("Moved stock to the spare fridge; engineer called.");
        issue.ResolvedById.ShouldBe(Bob);
        issue.ClosedAt.ShouldBe(Now.AddHours(2));
        issue.Events.Select(e => (e.Kind, e.From, e.To)).ShouldBe(
        [
            (IssueEventKind.Raised, null, "Medium"),
            (IssueEventKind.Assigned, null, Bob.ToString()),
            (IssueEventKind.StatusChanged, "Open", "InProgress"),
            (IssueEventKind.StatusChanged, "InProgress", "Resolved"),
            (IssueEventKind.StatusChanged, "Resolved", "Closed"),
        ]);
        issue.Events[^1].Note.ShouldBe("Checked, fridge back at 3°C.");
    }

    [Fact]
    public void Resolving_requires_a_corrective_action()
    {
        var issue = Raise();

        Should.Throw<InvalidOperationException>(() => issue.Resolve(Now, Bob, "  ", []));
        issue.Status.ShouldBe(IssueStatus.Open);
    }

    [Fact]
    public void Resolved_and_closed_issues_are_frozen_until_reopened()
    {
        var issue = Raise();
        issue.Resolve(Now, Bob, "Cleaned.", []);

        Should.Throw<InvalidOperationException>(() => issue.Assign(Now, Alice, Alice));
        Should.Throw<InvalidOperationException>(() => issue.ChangePriority(Now, Alice, IssuePriority.Low));
        Should.Throw<InvalidOperationException>(() => issue.SetDue(Now, Alice, null));
        Should.Throw<InvalidOperationException>(() => issue.Start(Now, Alice));
        Should.Throw<InvalidOperationException>(() => issue.Resolve(Now, Bob, "Again", []));

        Should.Throw<InvalidOperationException>(() => issue.Reopen(Now, Alice, ""));
        issue.Reopen(Now.AddHours(1), Alice, "Still dirty behind the oven.");

        issue.Status.ShouldBe(IssueStatus.Open);
        issue.Resolution.ShouldBeNull();
        issue.ResolvedAt.ShouldBeNull();
        issue.Events[^1].Note.ShouldBe("Still dirty behind the oven.");
    }

    [Fact]
    public void Managers_can_close_an_active_issue_directly_but_not_twice()
    {
        var issue = Raise();

        issue.Close(Now, Alice, "Duplicate of #12.");
        issue.Status.ShouldBe(IssueStatus.Closed);
        Should.Throw<InvalidOperationException>(() => issue.Close(Now, Alice, null));
        Should.Throw<InvalidOperationException>(() => Raise().Reopen(Now, Alice, "Not resolved yet"));
    }

    [Fact]
    public void Unchanged_values_record_nothing()
    {
        var issue = Raise();

        issue.Assign(Now, Alice, null);
        issue.ChangePriority(Now, Alice, IssuePriority.Medium);
        issue.SetDue(Now, Alice, Now.AddDays(1));

        issue.Events.Count.ShouldBe(1);
    }

    [Fact]
    public void Comments_need_text_or_a_photo()
    {
        var issue = Raise();

        Should.Throw<InvalidOperationException>(() => issue.Comment(Now, Bob, " ", []));
        issue.Comment(Now, Bob, "", [Guid.CreateVersion7()]);
        issue.Comment(Now, Bob, "  Engineer booked for 3pm.  ", []);

        issue.Events.Count(e => e.Kind == IssueEventKind.Comment).ShouldBe(2);
        issue.Events[^1].Note.ShouldBe("Engineer booked for 3pm.");
    }

    [Fact]
    public void Overdue_only_while_active()
    {
        var issue = Raise();

        issue.IsOverdue(Now.AddDays(2)).ShouldBeTrue();
        issue.IsOverdue(Now).ShouldBeFalse();
        issue.Resolve(Now, Bob, "Fixed.", []);
        issue.IsOverdue(Now.AddDays(2)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(IssuePriority.Critical, 4)]
    [InlineData(IssuePriority.High, 24)]
    [InlineData(IssuePriority.Medium, 72)]
    [InlineData(IssuePriority.Low, 168)]
    public void Default_response_times_scale_with_priority(IssuePriority priority, int hours) =>
        IssuePolicy.DefaultResponseTime(priority).ShouldBe(TimeSpan.FromHours(hours));
}
