using System.Net;
using Trail.Application.Common.Security;
using Trail.Application.Features.Tasks;
using Trail.Domain.Files;
using Trail.Domain.Organizations;
using Trail.Domain.Tasks;
using Trail.IntegrationTests.Infrastructure;
using static Trail.IntegrationTests.Tasks.TaskTestKit;

namespace Trail.IntegrationTests.Tasks;

public sealed class TaskExecutionTests(MySqlFixture mysql) : IAsyncLifetime
{
    private TrailApiFactory _factory = null!;
    private TaskTestKit _kit = null!;
    private TestMember _owner = null!;
    private TestMember _teamMember = null!;
    private HttpClient _ownerClient = null!;
    private HttpClient _memberClient = null!;
    private Site _site = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = await TrailApiFactory.CreateAsync(mysql);
        _owner = await _factory.CreateOrganizationAsync();
        _teamMember = await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.TeamMember);
        _ownerClient = _factory.CreateClient(_owner);
        _memberClient = _factory.CreateClient(_teamMember);
        _kit = new TaskTestKit(_factory, _owner.OrganizationId);
        _site = await _kit.AddSiteAsync("London");
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<SiteTask> ChecklistAsync(Action<SiteTask>? configure = null, Action<TaskTemplate>? template = null) =>
        NewTaskAsync(TaskKind.Checklist, configure, template, Temperature, Clean, Notes);

    private async Task<SiteTask> NewTaskAsync(
        TaskKind kind, Action<SiteTask>? configure, Action<TaskTemplate>? template, params TemplateField[] fields)
    {
        var t = await _kit.AddTemplateAsync(kind, configure: template, fields: fields);
        return await _kit.AddTaskAsync(_site, t, configure);
    }

    private static string Url(SiteTask task, string action = "") => $"/api/v1/tasks/{task.Id}{action}";

    // Checklist flow

    [Fact]
    public async Task A_checklist_is_saved_as_a_draft_then_completed()
    {
        var task = await ChecklistAsync();

        var progress = await _memberClient.PutJsonAsync(Url(task, "/progress"), Answers(("temp", Answer(3.5))));
        var draft = await progress.ReadAsync<TaskDetailResponse>();
        draft.Status.ShouldBe(SiteTaskStatus.Open);
        var entry = draft.Entries.ShouldHaveSingleItem();
        entry.SubmittedAt.ShouldBeNull();
        entry.Answers.ShouldContainKey("temp");

        // Progress again replaces the draft in the same entry.
        (await _memberClient.PutJsonAsync(Url(task, "/progress"), Answers(("temp", Answer(4)), ("clean", Answer(true)))))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _kit.ReloadAsync(task.Id)).Entries.ShouldHaveSingleItem().Answers.Count.ShouldBe(2);

        var done = await (await _memberClient.PostJsonAsync(Url(task, "/complete"), Answers(("notes", Answer("all good")))))
            .ReadAsync<TaskDetailResponse>();

        done.Status.ShouldBe(SiteTaskStatus.Completed);
        done.CompletedBy.ShouldBe("Test User");
        done.CompletedById.ShouldBe(_teamMember.UserId);
        done.CompletedLate.ShouldBeFalse();
        done.HasFailures.ShouldBeFalse();
        var submitted = done.Entries.ShouldHaveSingleItem();
        submitted.SubmittedAt.ShouldNotBeNull();
        submitted.SubmittedById.ShouldBe(_teamMember.UserId);
        submitted.Answers.Keys.Order().ShouldBe(["clean", "notes", "temp"]);
    }

    [Fact]
    public async Task Completing_without_required_answers_lists_each_missing_field()
    {
        var task = await ChecklistAsync();

        var response = await _memberClient.PostJsonAsync(Url(task, "/complete"), Answers(("temp", Answer(3))));

        await response.ShouldBeAsync(HttpStatusCode.BadRequest, "task.invalid_answers");
        (await ErrorKeysAsync(response)).ShouldBe(["answers.clean"]);
        (await _kit.ReloadAsync(task.Id)).Status.ShouldBe(SiteTaskStatus.Open);
    }

    [Fact]
    public async Task Progress_rejects_wrongly_shaped_and_unknown_answers_but_allows_gaps()
    {
        var task = await ChecklistAsync();

        var bad = await _memberClient.PutJsonAsync(Url(task, "/progress"),
            Answers(("temp", Answer("warm")), ("ghost", Answer(true))));

        await bad.ShouldBeAsync(HttpStatusCode.BadRequest, "task.invalid_answers");
        (await ErrorKeysAsync(bad)).Order().ShouldBe(["answers.ghost", "answers.temp"]);

        (await _memberClient.PutJsonAsync(Url(task, "/progress"), Answers()))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_failing_reading_needs_a_comment_to_complete_and_is_recorded_as_a_failure()
    {
        var task = await ChecklistAsync();

        // Mid-way a failing value is fine and already shows up as a failure.
        var draft = await (await _memberClient.PutJsonAsync(Url(task, "/progress"), Answers(("temp", Answer(9)))))
            .ReadAsync<TaskDetailResponse>();
        draft.Entries.Single().Failures.Single().FieldKey.ShouldBe("temp");

        var withoutComment = await _memberClient.PostJsonAsync(Url(task, "/complete"),
            Answers(("temp", Answer(9)), ("clean", Answer(true))));
        await withoutComment.ShouldBeAsync(HttpStatusCode.BadRequest, "task.invalid_answers");
        (await ErrorKeysAsync(withoutComment)).ShouldBe(["answers.temp"]);

        var done = await (await _memberClient.PostJsonAsync(Url(task, "/complete"),
                Answers(("temp", Answer(9, "Moved stock to the other fridge")), ("clean", Answer(true)))))
            .ReadAsync<TaskDetailResponse>();
        done.HasFailures.ShouldBeTrue();
        done.Entries.Single().Failures.Single().Reason.ShouldContain("above the maximum");
    }

    [Fact]
    public async Task The_body_wins_over_the_draft_when_completing()
    {
        var task = await ChecklistAsync();
        await _memberClient.PutJsonAsync(Url(task, "/progress"), Answers(("temp", Answer(9)), ("clean", Answer(false))));

        var done = await (await _memberClient.PostJsonAsync(Url(task, "/complete"), Answers(("temp", Answer(2)), ("clean", Answer(true)))))
            .ReadAsync<TaskDetailResponse>();

        done.HasFailures.ShouldBeFalse();
        done.Entries.Single().Answers["temp"].Value!.Value.GetDecimal().ShouldBe(2);
    }

    [Fact]
    public async Task A_form_is_completed_like_a_checklist()
    {
        var task = await NewTaskAsync(TaskKind.Form, null, null,
            new TemplateField { Key = "who", Label = "Who", Type = FieldType.Text },
            new TemplateField { Key = "what", Label = "What", Type = FieldType.LongText, Required = false });

        (await _memberClient.PostJsonAsync(Url(task, "/complete"), Answers(("what", Answer("x")))))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var done = await (await _memberClient.PostJsonAsync(Url(task, "/complete"), Answers("Handover", null, ("who", Answer("Sam")))))
            .ReadAsync<TaskDetailResponse>();

        done.Status.ShouldBe(SiteTaskStatus.Completed);
        done.Entries.Single().Note.ShouldBe("Handover");
    }

    [Fact]
    public async Task A_missed_task_inside_the_late_window_is_completed_late()
    {
        var task = await ChecklistAsync(t =>
        {
            t.Status = SiteTaskStatus.Missed;
            t.DueDate = TodayAt(_site).AddDays(-2);
            t.DueAt = Now.AddDays(-2);
        });

        var done = await (await _memberClient.PostJsonAsync(Url(task, "/complete"),
                Answers(("temp", Answer(3)), ("clean", Answer(true)))))
            .ReadAsync<TaskDetailResponse>();

        done.Status.ShouldBe(SiteTaskStatus.Completed);
        done.CompletedLate.ShouldBeTrue();
    }

    [Fact]
    public async Task A_missed_task_past_the_late_window_cannot_be_worked_on()
    {
        var task = await ChecklistAsync(t =>
        {
            t.Status = SiteTaskStatus.Missed;
            t.DueDate = TodayAt(_site).AddDays(-9);
            t.DueAt = Now.AddDays(-9);
        });

        await (await _memberClient.PostJsonAsync(Url(task, "/complete"), Answers(("temp", Answer(3)), ("clean", Answer(true)))))
            .ShouldBeAsync(HttpStatusCode.Conflict, "task.late_window_closed");
        await (await _memberClient.PutJsonAsync(Url(task, "/progress"), Answers()))
            .ShouldBeAsync(HttpStatusCode.Conflict, "task.late_window_closed");
    }

    [Fact]
    public async Task Completed_and_cancelled_tasks_are_not_open()
    {
        var done = await ChecklistAsync(t => t.Status = SiteTaskStatus.Completed);
        var cancelled = await ChecklistAsync(t => t.Status = SiteTaskStatus.Cancelled);

        foreach (var task in new[] { done, cancelled })
        {
            await (await _memberClient.PutJsonAsync(Url(task, "/progress"), Answers())).ShouldBeAsync(HttpStatusCode.Conflict, "task.not_open");
            await (await _memberClient.PostJsonAsync(Url(task, "/complete"), Answers())).ShouldBeAsync(HttpStatusCode.Conflict, "task.not_open");
        }
    }

    [Fact]
    public async Task A_task_that_is_not_available_yet_cannot_be_worked_on()
    {
        var task = await ChecklistAsync(t => t.AvailableAt = Now.AddHours(3));

        await (await _memberClient.PutJsonAsync(Url(task, "/progress"), Answers(("temp", Answer(3)))))
            .ShouldBeAsync(HttpStatusCode.Conflict, "task.not_available_yet");
        await (await _memberClient.PostJsonAsync(Url(task, "/complete"), Answers(("temp", Answer(3)), ("clean", Answer(true)))))
            .ShouldBeAsync(HttpStatusCode.Conflict, "task.not_available_yet");
    }

    // Record logs

    private Task<SiteTask> FridgeLogAsync(Action<TaskTemplate>? template = null) =>
        NewTaskAsync(TaskKind.RecordLog, null, template, Temperature);

    [Fact]
    public async Task A_record_log_collects_entries_then_completes()
    {
        var task = await FridgeLogAsync();

        var first = await (await _memberClient.PostJsonAsync(Url(task, "/entries"), Answers(("temp", Answer(3)))))
            .ReadAsync<TaskDetailResponse>();
        first.Status.ShouldBe(SiteTaskStatus.Open);
        first.Entries.ShouldHaveSingleItem().SubmittedAt.ShouldNotBeNull();
        first.HasFailures.ShouldBeFalse();

        var second = await (await _memberClient.PostJsonAsync(Url(task, "/entries"), Answers(("temp", Answer(8, "Door was left open")))))
            .ReadAsync<TaskDetailResponse>();
        second.Entries.Count.ShouldBe(2);
        second.HasFailures.ShouldBeTrue();

        var done = await (await _memberClient.PostJsonAsync(Url(task, "/complete"), new { note = "End of shift" }))
            .ReadAsync<TaskDetailResponse>();
        done.Status.ShouldBe(SiteTaskStatus.Completed);
        done.HasFailures.ShouldBeTrue();
        done.Entries.Count.ShouldBe(2);
        done.Entries.Count(e => e.Note == "End of shift").ShouldBe(1);
    }

    [Fact]
    public async Task A_record_log_cannot_complete_without_entries()
    {
        var task = await FridgeLogAsync();

        await (await _memberClient.PostJsonAsync(Url(task, "/complete"), new { }))
            .ShouldBeAsync(HttpStatusCode.Conflict, "task.no_entries");
    }

    [Fact]
    public async Task Record_log_entries_are_fully_validated()
    {
        var task = await FridgeLogAsync();

        var missing = await _memberClient.PostJsonAsync(Url(task, "/entries"), Answers());
        await missing.ShouldBeAsync(HttpStatusCode.BadRequest, "task.invalid_answers");
        (await ErrorKeysAsync(missing)).ShouldBe(["answers.temp"]);
        (await _kit.ReloadAsync(task.Id)).Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task Actions_only_apply_to_the_matching_kind_of_task()
    {
        var log = await FridgeLogAsync();
        var checklist = await ChecklistAsync();

        await (await _memberClient.PutJsonAsync(Url(log, "/progress"), Answers())).ShouldBeAsync(HttpStatusCode.Conflict, "task.wrong_kind");
        await (await _memberClient.PostJsonAsync(Url(checklist, "/entries"), Answers(("temp", Answer(3)))))
            .ShouldBeAsync(HttpStatusCode.Conflict, "task.wrong_kind");
    }

    // Photos

    [Fact]
    public async Task A_task_that_requires_a_photo_needs_one_to_complete()
    {
        var task = await ChecklistAsync(template: t => t.RequiresPhoto = true);
        var photo = await _kit.AddAttachmentAsync(_site);
        var answers = new[] { ("temp", Answer(3)), ("clean", Answer(true)) };

        var without = await _memberClient.PostJsonAsync(Url(task, "/complete"), Answers(null, null, answers));
        await without.ShouldBeAsync(HttpStatusCode.BadRequest, "task.photo_required");

        var done = await (await _memberClient.PostJsonAsync(Url(task, "/complete"), Answers(null, [photo.Id], answers)))
            .ReadAsync<TaskDetailResponse>();
        done.Entries.Single().PhotoIds.ShouldBe([photo.Id]);
    }

    [Fact]
    public async Task A_photo_saved_with_the_draft_counts_when_completing()
    {
        var task = await ChecklistAsync(template: t => t.RequiresPhoto = true);
        var photo = await _kit.AddAttachmentAsync(_site);

        await _memberClient.PutJsonAsync(Url(task, "/progress"), Answers(null, [photo.Id]));
        var done = await _memberClient.PostJsonAsync(Url(task, "/complete"), Answers(("temp", Answer(3)), ("clean", Answer(true))));

        (await done.ReadAsync<TaskDetailResponse>()).Status.ShouldBe(SiteTaskStatus.Completed);
    }

    [Fact]
    public async Task Every_record_log_entry_needs_a_photo_when_the_template_requires_one()
    {
        var task = await FridgeLogAsync(t => t.RequiresPhoto = true);
        var photo = await _kit.AddAttachmentAsync(_site);

        await (await _memberClient.PostJsonAsync(Url(task, "/entries"), Answers(("temp", Answer(3)))))
            .ShouldBeAsync(HttpStatusCode.BadRequest, "task.photo_required");
        (await _memberClient.PostJsonAsync(Url(task, "/entries"), Answers(null, [photo.Id], ("temp", Answer(3)))))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Photo_ids_must_be_photos_uploaded_for_the_tasks_site()
    {
        var task = await NewTaskAsync(TaskKind.Checklist, null, null, Clean,
            new TemplateField { Key = "pic", Label = "Evidence", Type = FieldType.Photo, Required = false },
            new TemplateField { Key = "sign", Label = "Signed", Type = FieldType.Signature, Required = false });
        var otherSite = await _kit.AddSiteAsync("Elsewhere");
        var foreign = await _kit.AddAttachmentAsync(otherSite);
        var document = await _kit.AddAttachmentAsync(_site, AttachmentKind.Document);
        var signature = await _kit.AddAttachmentAsync(_site, AttachmentKind.Signature);
        var photo = await _kit.AddAttachmentAsync(_site);

        object Body(string field, Guid id) => field == "sign"
            ? Answers(("clean", Answer(true)), ("sign", Answer(id.ToString())))
            : Answers(("clean", Answer(true)), ("pic", Answer(null, null, id)));

        foreach (var (field, id) in new[]
                 {
                     ("pic", foreign.Id), ("pic", Guid.NewGuid()), ("pic", document.Id), ("pic", signature.Id),
                     ("sign", photo.Id), ("sign", Guid.NewGuid()),
                 })
        {
            await (await _memberClient.PostJsonAsync(Url(task, "/complete"), Body(field, id)))
                .ShouldBeAsync(HttpStatusCode.BadRequest, "task.unknown_attachments");
            await (await _memberClient.PutJsonAsync(Url(task, "/progress"), Body(field, id)))
                .ShouldBeAsync(HttpStatusCode.BadRequest, "task.unknown_attachments");
        }

        await (await _memberClient.PostJsonAsync(Url(task, "/complete"),
                Answers(null, [Guid.NewGuid()], ("clean", Answer(true)))))
            .ShouldBeAsync(HttpStatusCode.BadRequest, "task.unknown_attachments");

        var done = await _memberClient.PostJsonAsync(Url(task, "/complete"),
            Answers(("clean", Answer(true)), ("pic", Answer(null, null, photo.Id)), ("sign", Answer(signature.Id.ToString()))));
        (await done.ReadAsync<TaskDetailResponse>()).Status.ShouldBe(SiteTaskStatus.Completed);
    }

    [Fact]
    public async Task Record_log_entries_check_their_photos_too()
    {
        var task = await FridgeLogAsync();
        var foreign = await _kit.AddAttachmentAsync(await _kit.AddSiteAsync("Elsewhere"));

        await (await _memberClient.PostJsonAsync(Url(task, "/entries"), Answers(null, [foreign.Id], ("temp", Answer(3)))))
            .ShouldBeAsync(HttpStatusCode.BadRequest, "task.unknown_attachments");
    }

    // Approval

    private Task<SiteTask> ApprovalChecklistAsync() => ChecklistAsync(template: t => t.RequiresApproval = true);

    private static object GoodAnswers => Answers(("temp", Answer(3)), ("clean", Answer(true)));

    [Fact]
    public async Task A_manager_approves_a_completed_task_once()
    {
        var task = await ApprovalChecklistAsync();
        var completed = await (await _memberClient.PostJsonAsync(Url(task, "/complete"), GoodAnswers)).ReadAsync<TaskDetailResponse>();
        completed.IsAwaitingApproval.ShouldBeTrue();

        var approved = await (await _ownerClient.PostJsonAsync(Url(task, "/approve"), new { note = "Checked on site" }))
            .ReadAsync<TaskDetailResponse>();

        approved.IsAwaitingApproval.ShouldBeFalse();
        approved.ApprovedById.ShouldBe(_owner.UserId);
        approved.ApprovedBy.ShouldBe("Test User");
        approved.ApprovalNote.ShouldBe("Checked on site");

        await (await _ownerClient.PostJsonAsync(Url(task, "/approve"), new { })).ShouldBeAsync(HttpStatusCode.Conflict, "task.not_awaiting_approval");
        await (await _ownerClient.PostJsonAsync(Url(task, "/reopen"), new { reason = "Too late" })).ShouldBeAsync(HttpStatusCode.Conflict, "task.cannot_reopen");
    }

    [Fact]
    public async Task Approving_does_not_need_a_body()
    {
        var task = await ApprovalChecklistAsync();
        await _memberClient.PostJsonAsync(Url(task, "/complete"), GoodAnswers);

        (await _ownerClient.PostEmptyAsync(Url(task, "/approve"))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Tasks_that_are_not_awaiting_approval_cannot_be_approved()
    {
        var open = await ApprovalChecklistAsync();
        var noApprovalNeeded = await ChecklistAsync();
        await _memberClient.PostJsonAsync(Url(noApprovalNeeded, "/complete"), GoodAnswers);

        await (await _ownerClient.PostJsonAsync(Url(open, "/approve"), new { })).ShouldBeAsync(HttpStatusCode.Conflict, "task.not_awaiting_approval");
        await (await _ownerClient.PostJsonAsync(Url(noApprovalNeeded, "/approve"), new { })).ShouldBeAsync(HttpStatusCode.Conflict, "task.not_awaiting_approval");
    }

    [Fact]
    public async Task Nobody_approves_their_own_work()
    {
        var task = await ApprovalChecklistAsync();
        await _ownerClient.PostJsonAsync(Url(task, "/complete"), GoodAnswers);

        await (await _ownerClient.PostJsonAsync(Url(task, "/approve"), new { })).ShouldBeAsync(HttpStatusCode.Forbidden, "task.self_approval");

        var manager = _factory.CreateClient(await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.SiteManager));
        (await manager.PostJsonAsync(Url(task, "/approve"), new { })).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_team_member_cannot_approve_or_reopen()
    {
        var task = await ApprovalChecklistAsync();
        await _ownerClient.PostJsonAsync(Url(task, "/complete"), GoodAnswers);

        (await _memberClient.PostJsonAsync(Url(task, "/approve"), new { })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _memberClient.PostJsonAsync(Url(task, "/reopen"), new { reason = "no" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_reopened_checklist_can_be_edited_and_completed_again()
    {
        var task = await ApprovalChecklistAsync();
        await _memberClient.PostJsonAsync(Url(task, "/complete"), GoodAnswers);

        var reopened = await (await _ownerClient.PostJsonAsync(Url(task, "/reopen"), new { reason = "Temperature looks wrong" }))
            .ReadAsync<TaskDetailResponse>();
        reopened.Status.ShouldBe(SiteTaskStatus.Open);
        reopened.CompletedAt.ShouldBeNull();
        reopened.ApprovalNote.ShouldBe("Temperature looks wrong");
        reopened.Entries.Single().SubmittedAt.ShouldBeNull();

        (await _memberClient.PutJsonAsync(Url(task, "/progress"), Answers(("temp", Answer(2)))))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        var again = await (await _memberClient.PostJsonAsync(Url(task, "/complete"), Answers(("clean", Answer(true)))))
            .ReadAsync<TaskDetailResponse>();

        again.Status.ShouldBe(SiteTaskStatus.Completed);
        again.Entries.ShouldHaveSingleItem().Answers["temp"].Value!.Value.GetDecimal().ShouldBe(2);
        again.IsAwaitingApproval.ShouldBeTrue();
    }

    [Fact]
    public async Task Reopening_needs_a_reason_and_a_completed_task()
    {
        var done = await ChecklistAsync();
        var open = await ChecklistAsync();
        await _memberClient.PostJsonAsync(Url(done, "/complete"), GoodAnswers);

        await (await _ownerClient.PostJsonAsync(Url(done, "/reopen"), new { reason = "" })).ShouldBeAsync(HttpStatusCode.BadRequest);
        await (await _ownerClient.PostJsonAsync(Url(done, "/reopen"), new { })).ShouldBeAsync(HttpStatusCode.BadRequest);
        await (await _ownerClient.PostJsonAsync(Url(open, "/reopen"), new { reason = "x" })).ShouldBeAsync(HttpStatusCode.Conflict, "task.cannot_reopen");
    }

    // Snooze

    [Fact]
    public async Task An_open_task_can_be_snoozed_to_a_later_day()
    {
        var task = await ChecklistAsync();
        var target = TodayAt(_site).AddDays(3);

        var snoozed = await (await _memberClient.PostJsonAsync(Url(task, "/snooze"), new { date = target }))
            .ReadAsync<TaskDetailResponse>();

        snoozed.DueDate.ShouldBe(target);
        snoozed.SnoozeCount.ShouldBe(1);
        snoozed.Status.ShouldBe(SiteTaskStatus.Open);
    }

    [Fact]
    public async Task Snooze_dates_are_limited()
    {
        var task = await ChecklistAsync();
        var today = TodayAt(_site);

        await (await _memberClient.PostJsonAsync(Url(task, "/snooze"), new { date = today })).ShouldBeAsync(HttpStatusCode.BadRequest, "task.invalid_snooze_date");
        await (await _memberClient.PostJsonAsync(Url(task, "/snooze"), new { date = today.AddDays(-1) })).ShouldBeAsync(HttpStatusCode.BadRequest, "task.invalid_snooze_date");
        await (await _memberClient.PostJsonAsync(Url(task, "/snooze"), new { date = today.AddDays(SiteTask.MaxSnoozeDays + 1) }))
            .ShouldBeAsync(HttpStatusCode.BadRequest, "task.invalid_snooze_date");
        (await _memberClient.PostJsonAsync(Url(task, "/snooze"), new { date = today.AddDays(SiteTask.MaxSnoozeDays) }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Only_open_tasks_can_be_snoozed()
    {
        var done = await ChecklistAsync(t => t.Status = SiteTaskStatus.Completed);
        var missed = await ChecklistAsync(t => { t.Status = SiteTaskStatus.Missed; t.DueDate = TodayAt(_site).AddDays(-1); t.DueAt = Now.AddDays(-1); });

        foreach (var task in new[] { done, missed })
            await (await _memberClient.PostJsonAsync(Url(task, "/snooze"), new { date = TodayAt(_site).AddDays(2) }))
                .ShouldBeAsync(HttpStatusCode.Conflict, "task.not_open");
    }

    // Ad hoc

    [Fact]
    public async Task An_ad_hoc_task_defaults_to_today_at_the_end_of_the_day()
    {
        var template = await _kit.AddTemplateAsync(fields: [Clean]);

        var response = await _memberClient.PostJsonAsync($"/api/v1/sites/{_site.Id}/tasks", new { templateId = template.Id });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var task = await response.ReadAsync<TaskDetailResponse>();
        response.Headers.Location!.ToString().ShouldEndWith($"/api/v1/tasks/{task.Id}");
        task.Source.ShouldBe(SiteTaskSource.AdHoc);
        task.DueDate.ShouldBe(TodayAt(_site));
        task.Status.ShouldBe(SiteTaskStatus.Open);
        task.Title.ShouldBe(template.Name);
        task.Fields.Single().Key.ShouldBe("clean");
        TimeZoneInfo.ConvertTimeFromUtc(task.DueAt, TimeZoneInfo.FindSystemTimeZoneById("Europe/London"))
            .TimeOfDay.ShouldBe(new TimeSpan(23, 59, 0));

        var list = await (await _memberClient.GetAsync($"/api/v1/sites/{_site.Id}/tasks", Ct)).ReadAsync<SiteTaskListResponse>();
        list.Items.ShouldContain(i => i.Id == task.Id && i.Source == SiteTaskSource.AdHoc);
    }

    [Fact]
    public async Task An_ad_hoc_task_can_be_planned_ahead_with_a_time_and_department()
    {
        var template = await _kit.AddTemplateAsync(fields: [Clean]);
        var department = await _kit.AddDepartmentAsync("Kitchen");
        var date = TodayAt(_site).AddDays(30);

        var task = await (await _ownerClient.PostJsonAsync($"/api/v1/sites/{_site.Id}/tasks",
                new { templateId = template.Id, date, dueTime = "14:30", departmentId = department.Id }))
            .ReadAsync<TaskDetailResponse>();

        task.DueDate.ShouldBe(date);
        task.DepartmentId.ShouldBe(department.Id);
        TimeZoneInfo.ConvertTimeFromUtc(task.DueAt, TimeZoneInfo.FindSystemTimeZoneById("Europe/London"))
            .TimeOfDay.ShouldBe(new TimeSpan(14, 30, 0));
    }

    [Fact]
    public async Task Ad_hoc_creation_is_validated()
    {
        var template = await _kit.AddTemplateAsync(fields: [Clean]);
        var inactive = await _kit.AddTemplateAsync(configure: t => t.IsActive = false, fields: [Clean]);
        var closed = await _kit.AddSiteAsync("Closed", isActive: false);
        var today = TodayAt(_site);
        var url = $"/api/v1/sites/{_site.Id}/tasks";

        await (await _ownerClient.PostJsonAsync(url, new { templateId = template.Id, date = today.AddDays(-1) })).ShouldBeAsync(HttpStatusCode.BadRequest, "task.invalid_date");
        await (await _ownerClient.PostJsonAsync(url, new { templateId = template.Id, date = today.AddDays(31) })).ShouldBeAsync(HttpStatusCode.BadRequest, "task.invalid_date");
        await (await _ownerClient.PostJsonAsync(url, new { templateId = Guid.NewGuid() })).ShouldBeAsync(HttpStatusCode.BadRequest, "task.unknown_template");
        await (await _ownerClient.PostJsonAsync(url, new { templateId = inactive.Id })).ShouldBeAsync(HttpStatusCode.BadRequest, "task.template_inactive");
        await (await _ownerClient.PostJsonAsync(url, new { templateId = template.Id, departmentId = Guid.NewGuid() })).ShouldBeAsync(HttpStatusCode.BadRequest, "task.unknown_department");
        await (await _ownerClient.PostJsonAsync(url, new { })).ShouldBeAsync(HttpStatusCode.BadRequest);
        await (await _ownerClient.PostJsonAsync($"/api/v1/sites/{closed.Id}/tasks", new { templateId = template.Id })).ShouldBeAsync(HttpStatusCode.Conflict, "site.inactive");
        await (await _ownerClient.PostJsonAsync($"/api/v1/sites/{Guid.NewGuid()}/tasks", new { templateId = template.Id })).ShouldBeAsync(HttpStatusCode.NotFound, "site.not_found");
    }
}
