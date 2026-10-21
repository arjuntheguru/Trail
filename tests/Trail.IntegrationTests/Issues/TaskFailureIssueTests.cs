using Trail.Application.Common.Paging;
using Trail.Application.Common.Security;
using Trail.Application.Features.Issues;
using Trail.Domain.Issues;
using Trail.Domain.Organizations;
using Trail.Domain.Tasks;
using Trail.IntegrationTests.Infrastructure;
using Trail.IntegrationTests.Tasks;
using static Trail.IntegrationTests.Tasks.TaskTestKit;

namespace Trail.IntegrationTests.Issues;

/// <summary>Failing answers submitted through the task API raise issues in the same save.</summary>
public sealed class TaskFailureIssueTests(MySqlFixture mysql) : IAsyncLifetime
{
    private TrailApiFactory _factory = null!;
    private TaskTestKit _kit = null!;
    private HttpClient _ownerClient = null!;
    private HttpClient _memberClient = null!;
    private Site _site = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = await TrailApiFactory.CreateAsync(mysql);
        var owner = await _factory.CreateOrganizationAsync();
        _ownerClient = _factory.CreateClient(owner);
        _memberClient = _factory.CreateClient(await _factory.AddMemberAsync(owner.OrganizationId, SystemRoles.TeamMember));
        _kit = new TaskTestKit(_factory, owner.OrganizationId);
        _site = await _kit.AddSiteAsync("London");
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<IReadOnlyList<IssueListItemResponse>> IssuesForAsync(SiteTask task) =>
        (await (await _ownerClient.GetAsync($"/api/v1/issues?siteTaskId={task.Id}", Ct))
            .ReadAsync<PagedResult<IssueListItemResponse>>()).Items;

    [Fact]
    public async Task Completing_a_checklist_with_a_failure_raises_an_issue_and_a_pass_raises_none()
    {
        var template = await _kit.AddTemplateAsync(TaskKind.Checklist, fields: [Temperature, Clean]);
        var failing = await _kit.AddTaskAsync(_site, template);
        var passing = await _kit.AddTaskAsync(_site, template);

        // A failing value saved as a draft is not final, so it raises nothing yet.
        await _memberClient.PutJsonAsync($"/api/v1/tasks/{failing.Id}/progress", Answers(("temp", Answer(9))));
        (await IssuesForAsync(failing)).ShouldBeEmpty();

        await (await _memberClient.PostJsonAsync($"/api/v1/tasks/{failing.Id}/complete",
            Answers(("temp", Answer(9, "Moved stock")), ("clean", Answer(true))))).ReadAsync<object>();
        await (await _memberClient.PostJsonAsync($"/api/v1/tasks/{passing.Id}/complete",
            Answers(("temp", Answer(3)), ("clean", Answer(true))))).ReadAsync<object>();

        var issue = (await IssuesForAsync(failing)).ShouldHaveSingleItem();
        issue.Source.ShouldBe(IssueSource.TaskFailure);
        issue.Priority.ShouldBe(IssuePriority.High);
        issue.SiteId.ShouldBe(_site.Id);
        issue.Title.ShouldStartWith("Fridge temperature:");
        (await IssuesForAsync(passing)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Repeated_bad_readings_on_a_log_add_to_one_issue()
    {
        var template = await _kit.AddTemplateAsync(TaskKind.RecordLog, "Fridge log", fields: [Temperature]);
        var task = await _kit.AddTaskAsync(_site, template);

        foreach (var reading in new[] { 8, 3, 10 })
        {
            await (await _memberClient.PostJsonAsync($"/api/v1/tasks/{task.Id}/entries",
                Answers(("temp", Answer(reading, "Door left open"))))).ReadAsync<object>();
        }
        await (await _memberClient.PostJsonAsync($"/api/v1/tasks/{task.Id}/complete", new { })).ReadAsync<object>();

        var issue = (await IssuesForAsync(task)).ShouldHaveSingleItem();
        var detail = await (await _ownerClient.GetAsync($"/api/v1/issues/{issue.Id}", Ct)).ReadAsync<IssueDetailResponse>();
        detail.Timeline.Select(e => e.Kind).ShouldBe([IssueEventKind.Raised, IssueEventKind.Comment]);
        detail.Timeline[1].Note!.ShouldStartWith("Failed again: 10°C");
    }
}
