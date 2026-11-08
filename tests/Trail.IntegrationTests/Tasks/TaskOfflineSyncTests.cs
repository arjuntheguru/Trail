using System.Net;

using Trail.Application.Common.Security;
using Trail.Application.Features.Tasks;
using Trail.Domain.Organizations;
using Trail.Domain.Tasks;
using Trail.IntegrationTests.Infrastructure;

using static Trail.IntegrationTests.Tasks.TaskTestKit;

namespace Trail.IntegrationTests.Tasks;

/// <summary>Replay safety and client-supplied work times, for tasks completed or recorded on a phone that was offline.</summary>
public sealed class TaskOfflineSyncTests(MySqlFixture mysql) : IAsyncLifetime
{
    private TrailApiFactory _factory = null!;
    private TaskTestKit _kit = null!;
    private TestMember _owner = null!;
    private HttpClient _ownerClient = null!;
    private HttpClient _memberClient = null!;
    private Site _site = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = await TrailApiFactory.CreateAsync(mysql);
        _owner = await _factory.CreateOrganizationAsync();
        var member = await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.TeamMember);
        _ownerClient = _factory.CreateClient(_owner);
        _memberClient = _factory.CreateClient(member);
        _kit = new TaskTestKit(_factory, _owner.OrganizationId);
        _site = await _kit.AddSiteAsync("London");
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private Task<SiteTask> ChecklistAsync(Action<SiteTask>? configure = null) => NewTaskAsync(TaskKind.Checklist, configure);

    private async Task<SiteTask> NewTaskAsync(TaskKind kind, Action<SiteTask>? configure = null)
    {
        var template = await _kit.AddTemplateAsync(kind, fields: [Temperature, Clean]);
        return await _kit.AddTaskAsync(_site, template, configure);
    }

    private static string Url(SiteTask task, string action = "") => $"/api/v1/tasks/{task.Id}{action}";

    private static object Complete(Guid? requestId = null, DateTime? completedAt = null) => new
    {
        answers = new Dictionary<string, object> { ["temp"] = Answer(3), ["clean"] = Answer(true) },
        requestId,
        completedAt,
    };

    private static object Reading(Guid? requestId = null, DateTime? recordedAt = null) => new
    {
        answers = new Dictionary<string, object> { ["temp"] = Answer(3), ["clean"] = Answer(true) },
        requestId,
        recordedAt,
    };

    // Complete

    [Fact]
    public async Task Completing_twice_with_the_same_request_id_completes_once()
    {
        var task = await ChecklistAsync();
        var requestId = Guid.NewGuid();

        var first = await (await _memberClient.PostJsonAsync(Url(task, "/complete"), Complete(requestId))).ReadAsync<TaskDetailResponse>();
        var response = await _memberClient.PostJsonAsync(Url(task, "/complete"), Complete(requestId));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var second = await response.ReadAsync<TaskDetailResponse>();
        second.Status.ShouldBe(SiteTaskStatus.Completed);
        second.CompletedAt!.Value.ShouldBe(first.CompletedAt!.Value, TimeSpan.FromMilliseconds(1));
        (await _kit.ReloadAsync(task.Id)).Entries.Count.ShouldBe(1);

        await (await _memberClient.PostJsonAsync(Url(task, "/complete"), Complete(Guid.NewGuid())))
            .ShouldBeAsync(HttpStatusCode.Conflict, "task.not_open");
    }

    [Fact]
    public async Task A_replay_after_the_task_was_reopened_does_not_complete_it_again()
    {
        var task = await ChecklistAsync();
        var requestId = Guid.NewGuid();
        await _memberClient.PostJsonAsync(Url(task, "/complete"), Complete(requestId));
        await _ownerClient.PostJsonAsync(Url(task, "/reopen"), new { reason = "Check again" });

        var replay = await _memberClient.PostJsonAsync(Url(task, "/complete"), Complete(requestId));

        replay.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await replay.ReadAsync<TaskDetailResponse>()).Status.ShouldBe(SiteTaskStatus.Open);
        var stored = await _kit.ReloadAsync(task.Id);
        stored.Status.ShouldBe(SiteTaskStatus.Open);
        stored.CompletedAt.ShouldBeNull();
        stored.CompletionRequestId.ShouldBe(requestId);
    }

    [Fact]
    public async Task An_offline_completion_keeps_its_own_time_and_is_not_late_even_if_the_task_was_marked_missed()
    {
        var task = await ChecklistAsync(t =>
        {
            t.Status = SiteTaskStatus.Missed;
            t.DueAt = Now.AddHours(-2);
        });
        var doneAt = Now.AddHours(-3);

        var done = await (await _memberClient.PostJsonAsync(Url(task, "/complete"), Complete(completedAt: doneAt)))
            .ReadAsync<TaskDetailResponse>();

        done.Status.ShouldBe(SiteTaskStatus.Completed);
        done.CompletedAt!.Value.ShouldBe(doneAt, TimeSpan.FromSeconds(1));
        done.CompletedLate.ShouldBeFalse();
        done.SyncedAt.ShouldNotBeNull();
        done.Entries.Single().SubmittedAt!.Value.ShouldBe(doneAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task A_completion_without_an_offline_time_has_no_sync_time()
    {
        var task = await ChecklistAsync();

        var done = await (await _memberClient.PostJsonAsync(Url(task, "/complete"), Complete())).ReadAsync<TaskDetailResponse>();

        done.SyncedAt.ShouldBeNull();
    }

    [Fact]
    public async Task A_completion_time_outside_the_allowed_range_is_rejected_and_a_small_future_skew_is_clamped()
    {
        var tooOld = await ChecklistAsync();
        var future = await ChecklistAsync();
        var skewed = await ChecklistAsync();

        await (await _memberClient.PostJsonAsync(Url(tooOld, "/complete"), Complete(completedAt: Now.AddHours(-25))))
            .ShouldBeAsync(HttpStatusCode.BadRequest, "task.completed_at_out_of_range");
        await (await _memberClient.PostJsonAsync(Url(future, "/complete"), Complete(completedAt: Now.AddMinutes(10))))
            .ShouldBeAsync(HttpStatusCode.BadRequest, "task.completed_at_out_of_range");

        var before = Now;
        var done = await (await _memberClient.PostJsonAsync(Url(skewed, "/complete"), Complete(completedAt: Now.AddMinutes(1))))
            .ReadAsync<TaskDetailResponse>();
        done.CompletedAt!.Value.ShouldBeLessThanOrEqualTo(Now);
        done.CompletedAt!.Value.ShouldBeGreaterThanOrEqualTo(before.AddSeconds(-1));
    }

    [Fact]
    public async Task Another_organization_cannot_use_a_replay_to_read_a_task()
    {
        var task = await ChecklistAsync();
        var requestId = Guid.NewGuid();
        await _memberClient.PostJsonAsync(Url(task, "/complete"), Complete(requestId));
        var intruder = _factory.CreateClient(await _factory.CreateOrganizationAsync("Other Org"));

        await (await intruder.PostJsonAsync(Url(task, "/complete"), Complete(requestId)))
            .ShouldBeAsync(HttpStatusCode.NotFound, "task.not_found");
        await (await intruder.PostJsonAsync(Url(task, "/entries"), Reading(requestId)))
            .ShouldBeAsync(HttpStatusCode.NotFound, "task.not_found");
    }

    // Entries

    [Fact]
    public async Task Adding_an_entry_twice_with_the_same_request_id_adds_one_entry()
    {
        var task = await NewTaskAsync(TaskKind.RecordLog);
        var requestId = Guid.NewGuid();

        await _memberClient.PostJsonAsync(Url(task, "/entries"), Reading(requestId));
        var replay = await _memberClient.PostJsonAsync(Url(task, "/entries"), Reading(requestId));

        replay.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await replay.ReadAsync<TaskDetailResponse>()).Entries.Count.ShouldBe(1);
        (await _kit.ReloadAsync(task.Id)).Entries.Single().RequestId.ShouldBe(requestId);

        await _memberClient.PostJsonAsync(Url(task, "/entries"), Reading(Guid.NewGuid()));
        (await _kit.ReloadAsync(task.Id)).Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task An_entry_keeps_its_recorded_time_and_out_of_range_times_are_rejected()
    {
        var task = await NewTaskAsync(TaskKind.RecordLog);
        var recordedAt = Now.AddHours(-5);

        var added = await (await _memberClient.PostJsonAsync(Url(task, "/entries"), Reading(recordedAt: recordedAt)))
            .ReadAsync<TaskDetailResponse>();

        var entry = added.Entries.Single();
        entry.SubmittedAt!.Value.ShouldBe(recordedAt, TimeSpan.FromSeconds(1));
        entry.SyncedAt.ShouldNotBeNull();

        await (await _memberClient.PostJsonAsync(Url(task, "/entries"), Reading(recordedAt: Now.AddHours(-25))))
            .ShouldBeAsync(HttpStatusCode.BadRequest, "task.recorded_at_out_of_range");
        await (await _memberClient.PostJsonAsync(Url(task, "/entries"), Reading(recordedAt: Now.AddMinutes(30))))
            .ShouldBeAsync(HttpStatusCode.BadRequest, "task.recorded_at_out_of_range");
    }
}