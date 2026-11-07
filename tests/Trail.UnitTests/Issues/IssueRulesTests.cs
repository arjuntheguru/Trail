using Trail.Application.Common.Security;
using Trail.Application.Features.Issues;

namespace Trail.UnitTests.Issues;

public sealed class IssueRulesTests
{
    [Fact]
    public void Only_roles_holding_issues_view_can_be_assignees()
    {
        IssueRules.CanViewIssues([Permissions.Issues.View, Permissions.Issues.Raise]).ShouldBeTrue();
        IssueRules.CanViewIssues([Permissions.Issues.Raise]).ShouldBeFalse();
        IssueRules.CanViewIssues([]).ShouldBeFalse();
    }

    [Fact]
    public void Task_errors_distinguish_unknown_from_wrong_site()
    {
        IssueRules.UnknownTask.Code.ShouldBe("issue.unknown_task");
        IssueRules.TaskSiteMismatch.Code.ShouldBe("issue.task_site_mismatch");
        IssueRules.UnknownTask.Type.ShouldNotBe(IssueRules.TaskSiteMismatch.Type);
    }

    [Fact]
    public void Raise_command_validates_without_a_task()
    {
        var validator = new RaiseIssueValidator();
        var command = new RaiseIssueCommand("Broken fridge", null, Guid.NewGuid(), null, null, null, null, null);

        validator.Validate(command).IsValid.ShouldBeTrue();
        validator.Validate(command with { SiteTaskId = Guid.NewGuid() }).IsValid.ShouldBeTrue();
        validator.Validate(command with { Title = "" }).IsValid.ShouldBeFalse();
    }
}