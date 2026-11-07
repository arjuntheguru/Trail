using Trail.Application.Features.Issues;
using Trail.Domain.Issues;

namespace Trail.UnitTests.Issues;

public sealed class IssueRaiserTests
{
    private static readonly Guid TaskId = Guid.CreateVersion7();

    private static Issue Active(string field, Guid? taskId = null, bool resolved = false)
    {
        var issue = new Issue { Title = "x", SiteId = Guid.CreateVersion7(), SiteTaskId = taskId ?? TaskId, FieldKey = field };
        issue.Raised(DateTime.UtcNow, null, null);
        if (resolved) issue.Resolve(DateTime.UtcNow, Guid.CreateVersion7(), "fixed", []);
        return issue;
    }

    [Fact]
    public void An_issue_added_earlier_in_the_unit_of_work_counts_as_active()
    {
        var unsaved = Active("temp");

        var found = IssueRaiser.WithUnsaved([], [unsaved], TaskId, ["temp"]);

        found.ShouldBe([unsaved]);
    }

    [Fact]
    public void A_tracked_issue_that_is_also_saved_is_listed_once()
    {
        var saved = Active("temp");

        IssueRaiser.WithUnsaved([saved], [saved], TaskId, ["temp"]).Count.ShouldBe(1);
    }

    [Fact]
    public void Tracked_issues_of_other_tasks_other_fields_or_finished_are_ignored()
    {
        var tracked = new[] { Active("temp", Guid.CreateVersion7()), Active("other"), Active("temp", resolved: true) };

        IssueRaiser.WithUnsaved([], tracked, TaskId, ["temp"]).ShouldBeEmpty();
    }
}
