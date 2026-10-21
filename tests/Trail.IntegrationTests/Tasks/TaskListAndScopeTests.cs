using System.Net;
using Trail.Application.Common.Paging;
using Trail.Application.Common.Security;
using Trail.Application.Features.Tasks;
using Trail.Domain.Common;
using Trail.Domain.Organizations;
using Trail.Domain.Tasks;
using Trail.IntegrationTests.Infrastructure;
using static Trail.IntegrationTests.Tasks.TaskTestKit;

namespace Trail.IntegrationTests.Tasks;

public sealed class TaskListAndScopeTests(MySqlFixture mysql) : IAsyncLifetime
{
    private TrailApiFactory _factory = null!;
    private TaskTestKit _kit = null!;
    private TestMember _owner = null!;
    private HttpClient _ownerClient = null!;
    private TaskTemplate _template = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = await TrailApiFactory.CreateAsync(mysql);
        _owner = await _factory.CreateOrganizationAsync();
        _ownerClient = _factory.CreateClient(_owner);
        _kit = new TaskTestKit(_factory, _owner.OrganizationId);
        _template = await _kit.AddTemplateAsync(fields: [Temperature, Clean]);
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string SiteTasks(Site site, string query = "") => $"/api/v1/sites/{site.Id}/tasks{query}";

    private static Task<SiteTaskListResponse> ListAsync(HttpClient client, Site site, string query = "") =>
        client.GetAsync(SiteTasks(site, query), Ct).ContinueWith(t => t.Result.ReadAsync<SiteTaskListResponse>()).Unwrap();

    // The day list

    [Fact]
    public async Task The_default_date_is_each_sites_own_today()
    {
        // Auckland and Honolulu are 22-23 hours apart, so their local dates always differ.
        var auckland = await _kit.AddSiteAsync("Auckland", "Pacific/Auckland");
        var honolulu = await _kit.AddSiteAsync("Honolulu", "Pacific/Honolulu");
        var aucklandTask = await _kit.AddTaskAsync(auckland, _template);
        var honoluluTask = await _kit.AddTaskAsync(honolulu, _template);

        var a = await ListAsync(_ownerClient, auckland);
        var h = await ListAsync(_ownerClient, honolulu);

        a.Date.ShouldBe(SiteTime.Today(Now, SiteTime.Zone("Pacific/Auckland")));
        h.Date.ShouldBe(SiteTime.Today(Now, SiteTime.Zone("Pacific/Honolulu")));
        a.Date.ShouldNotBe(h.Date);
        a.TimeZone.ShouldBe("Pacific/Auckland");
        a.Items.Select(i => i.Id).ShouldBe([aucklandTask.Id]);
        h.Items.Select(i => i.Id).ShouldBe([honoluluTask.Id]);
    }

    [Fact]
    public async Task A_date_can_be_requested_and_items_are_ordered_by_due_time_then_title()
    {
        var site = await _kit.AddSiteAsync("London");
        var tomorrow = TodayAt(site).AddDays(1);
        var due = Now.Date.AddDays(1);
        await _kit.AddTaskAsync(site, _template, t => { t.Title = "B later"; t.DueDate = tomorrow; t.DueAt = due.AddHours(12); });
        await _kit.AddTaskAsync(site, _template, t => { t.Title = "B early"; t.DueDate = tomorrow; t.DueAt = due.AddHours(8); });
        await _kit.AddTaskAsync(site, _template, t => { t.Title = "A early"; t.DueDate = tomorrow; t.DueAt = due.AddHours(8); });
        await _kit.AddTaskAsync(site, _template, t => t.Title = "Today only");

        var list = await ListAsync(_ownerClient, site, $"?date={tomorrow:yyyy-MM-dd}");

        list.Date.ShouldBe(tomorrow);
        list.Items.Select(i => i.Title).ShouldBe(["A early", "B early", "B later"]);
    }

    [Fact]
    public async Task Items_carry_the_flags_a_client_needs()
    {
        var site = await _kit.AddSiteAsync("London");
        var overdue = await _kit.AddTaskAsync(site, _template, t => { t.Title = "Overdue"; t.DueAt = Now.AddMinutes(-30); });
        var later = await _kit.AddTaskAsync(site, _template, t =>
        {
            t.Title = "Later"; t.RequiresPhoto = true; t.AvailableAt = Now.AddHours(1); t.DueAt = Now.AddHours(5);
        });
        var awaiting = await _kit.AddTaskAsync(site, _template, t =>
        {
            t.Title = "Waiting"; t.RequiresApproval = true; t.Status = SiteTaskStatus.Completed; t.CompletedAt = Now;
            t.CompletedById = _owner.UserId; t.HasFailures = true; t.DueAt = Now.AddHours(6);
        });

        var items = (await ListAsync(_ownerClient, site)).Items.ToDictionary(i => i.Title);

        items["Overdue"].IsOverdue.ShouldBeTrue();
        items["Overdue"].CanCompleteLate.ShouldBeFalse();
        items["Later"].IsOverdue.ShouldBeFalse();
        items["Later"].RequiresPhoto.ShouldBeTrue();
        items["Later"].AvailableAt.ShouldNotBeNull();
        var waiting = items["Waiting"];
        waiting.IsAwaitingApproval.ShouldBeTrue();
        waiting.IsOverdue.ShouldBeFalse();
        waiting.HasFailures.ShouldBeTrue();
        waiting.CompletedBy.ShouldBe("Test User");
        waiting.EntryCount.ShouldBe(0);
        waiting.Id.ShouldBe(awaiting.Id);
        overdue.Id.ShouldBe(items["Overdue"].Id);
        later.Id.ShouldBe(items["Later"].Id);
    }

    [Fact]
    public async Task Todays_list_includes_missed_tasks_that_can_still_be_completed_late()
    {
        var site = await _kit.AddSiteAsync("London");
        var today = TodayAt(site);
        await _kit.AddTaskAsync(site, _template, t => t.Title = "Today");
        await _kit.AddTaskAsync(site, _template, t =>
        {
            t.Title = "Missed recently"; t.Status = SiteTaskStatus.Missed; t.DueDate = today.AddDays(-2); t.DueAt = Now.AddDays(-2);
        });
        await _kit.AddTaskAsync(site, _template, t =>
        {
            t.Title = "Missed long ago"; t.Status = SiteTaskStatus.Missed; t.DueDate = today.AddDays(-10); t.DueAt = Now.AddDays(-10);
        });
        await _kit.AddTaskAsync(site, _template, t =>
        {
            t.Title = "Done yesterday"; t.Status = SiteTaskStatus.Completed; t.DueDate = today.AddDays(-1); t.DueAt = Now.AddDays(-1);
        });

        var items = (await ListAsync(_ownerClient, site)).Items;

        items.Select(i => i.Title).ShouldBe(["Missed recently", "Today"]);
        items[0].CanCompleteLate.ShouldBeTrue();
        items[1].CanCompleteLate.ShouldBeFalse();
    }

    [Fact]
    public async Task The_list_can_be_limited_to_a_department()
    {
        var site = await _kit.AddSiteAsync("London");
        var kitchen = await _kit.AddDepartmentAsync("Kitchen");
        var bar = await _kit.AddDepartmentAsync("Bar");
        await _kit.AddTaskAsync(site, _template, t => { t.Title = "Kitchen job"; t.DepartmentId = kitchen.Id; });
        await _kit.AddTaskAsync(site, _template, t => { t.Title = "Bar job"; t.DepartmentId = bar.Id; });
        await _kit.AddTaskAsync(site, _template, t => t.Title = "Anyone");

        (await ListAsync(_ownerClient, site, $"?departmentId={kitchen.Id}")).Items.Select(i => i.Title).ShouldBe(["Kitchen job"]);
        (await ListAsync(_ownerClient, site)).Items.Count.ShouldBe(3);
    }

    // Cross-site list and detail

    [Fact]
    public async Task The_cross_site_list_filters_and_pages()
    {
        var one = await _kit.AddSiteAsync("One");
        var two = await _kit.AddSiteAsync("Two");
        await _kit.AddTaskAsync(one, _template, t => t.Title = "open");
        await _kit.AddTaskAsync(one, _template, t => { t.Title = "failed"; t.Status = SiteTaskStatus.Completed; t.HasFailures = true; });
        await _kit.AddTaskAsync(two, _template, t =>
        {
            t.Title = "awaiting"; t.Status = SiteTaskStatus.Completed; t.RequiresApproval = true;
        });

        async Task<string[]> Titles(string query) =>
            (await (await _ownerClient.GetAsync($"/api/v1/tasks{query}", Ct)).ReadAsync<PagedResult<TaskListItemResponse>>())
            .Items.Select(i => i.Title).Order().ToArray();

        (await Titles("")).ShouldBe(["awaiting", "failed", "open"]);
        (await Titles($"?siteId={two.Id}")).ShouldBe(["awaiting"]);
        (await Titles("?status=Completed")).ShouldBe(["awaiting", "failed"]);
        (await Titles("?hasFailures=true")).ShouldBe(["failed"]);
        (await Titles("?awaitingApproval=true")).ShouldBe(["awaiting"]);
        (await Titles("?awaitingApproval=false")).ShouldBe(["failed", "open"]);
        (await Titles($"?from={TodayAt(one).AddDays(1):yyyy-MM-dd}&to={TodayAt(one).AddDays(5):yyyy-MM-dd}")).ShouldBeEmpty();

        var paged = await (await _ownerClient.GetAsync("/api/v1/tasks?pageSize=2", Ct)).ReadAsync<PagedResult<TaskListItemResponse>>();
        paged.Items.Count.ShouldBe(2);
        paged.TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task The_cross_site_date_range_is_capped()
    {
        var from = new DateOnly(2026, 1, 1);

        await (await _ownerClient.GetAsync($"/api/v1/tasks?from={from:yyyy-MM-dd}&to={from.AddDays(93):yyyy-MM-dd}", Ct))
            .ShouldBeAsync(HttpStatusCode.BadRequest, "task.invalid_range");
        await (await _ownerClient.GetAsync($"/api/v1/tasks?from={from.AddDays(5):yyyy-MM-dd}&to={from:yyyy-MM-dd}", Ct))
            .ShouldBeAsync(HttpStatusCode.BadRequest, "task.invalid_range");
        (await _ownerClient.GetAsync($"/api/v1/tasks?from={from:yyyy-MM-dd}&to={from.AddDays(92):yyyy-MM-dd}", Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_detail_shows_entries_with_who_submitted_them()
    {
        var site = await _kit.AddSiteAsync("London");
        var log = await _kit.AddTemplateAsync(TaskKind.RecordLog, "Fridge log", fields: [Temperature]);
        var task = await _kit.AddTaskAsync(site, log);
        await _ownerClient.PostJsonAsync($"/api/v1/tasks/{task.Id}/entries", Answers(("temp", Answer(2))));
        await _ownerClient.PostJsonAsync($"/api/v1/tasks/{task.Id}/entries", Answers(("temp", Answer(7, "Restocked"))));

        var detail = await (await _ownerClient.GetAsync($"/api/v1/tasks/{task.Id}", Ct)).ReadAsync<TaskDetailResponse>();

        detail.SiteName.ShouldBe("London");
        detail.Fields.Single().Key.ShouldBe("temp");
        detail.Entries.Count.ShouldBe(2);
        detail.Entries.ShouldAllBe(e => e.SubmittedBy == "Test User" && e.SubmittedAt != null);
        detail.Entries.Count(e => e.HasFailures).ShouldBe(1);
        (await _ownerClient.GetAsync($"/api/v1/tasks/{Guid.NewGuid()}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // Site scope, permissions, tenancy

    [Fact]
    public async Task A_member_scoped_to_one_site_cannot_reach_another_sites_tasks()
    {
        var a = await _kit.AddSiteAsync("Site A");
        var b = await _kit.AddSiteAsync("Site B");
        var taskA = await _kit.AddTaskAsync(a, _template);
        var taskB = await _kit.AddTaskAsync(b, _template, t => t.RequiresApproval = true);
        var member = await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.SiteManager, allSites: false);
        await _ownerClient.SetAccessAsync(member.MembershipId, false, siteIds: [a.Id]);
        var client = _factory.CreateClient(member);

        (await ListAsync(client, a)).Items.Select(i => i.Id).ShouldBe([taskA.Id]);
        (await client.GetAsync($"/api/v1/tasks/{taskA.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await (await client.GetAsync("/api/v1/tasks", Ct)).ReadAsync<PagedResult<TaskListItemResponse>>())
            .Items.Select(i => i.Id).ShouldBe([taskA.Id]);

        var template = await _kit.AddTemplateAsync(fields: [Clean]);
        var id = taskB.Id;
        (await client.GetAsync(SiteTasks(b), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/v1/tasks/{id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/v1/tasks?siteId={b.Id}", Ct).ContinueWith(t => t.Result.ReadAsync<PagedResult<TaskListItemResponse>>()).Unwrap())
            .Items.ShouldBeEmpty();
        (await client.PutJsonAsync($"/api/v1/tasks/{id}/progress", Answers())).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PostJsonAsync($"/api/v1/tasks/{id}/complete", Answers())).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PostJsonAsync($"/api/v1/tasks/{id}/entries", Answers())).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PostJsonAsync($"/api/v1/tasks/{id}/approve", new { })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PostJsonAsync($"/api/v1/tasks/{id}/reopen", new { reason = "x" })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PostJsonAsync($"/api/v1/tasks/{id}/snooze", new { date = TodayAt(b).AddDays(1) })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PostJsonAsync(SiteTasks(b), new { templateId = template.Id })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _kit.ReloadAsync(id)).Status.ShouldBe(SiteTaskStatus.Open);
    }

    [Fact]
    public async Task Requests_without_the_right_permission_or_sign_in_are_refused()
    {
        var site = await _kit.AddSiteAsync("London");
        var task = await _kit.AddTaskAsync(site, _template);
        var role = await _kit.InDbAsync(async db =>
        {
            var r = new Trail.Domain.Identity.Role { Name = "Sites only", Permissions = [Permissions.Sites.View] };
            db.Roles.Add(r);
            await db.SaveChangesAsync();
            return r;
        });
        var noTasks = _factory.CreateClient(await _factory.AddMemberAsync(_owner.OrganizationId, role.Name));

        (await noTasks.GetAsync(SiteTasks(site), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await noTasks.GetAsync($"/api/v1/tasks/{task.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await noTasks.PostJsonAsync($"/api/v1/tasks/{task.Id}/complete", Answers())).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await noTasks.PostJsonAsync(SiteTasks(site), new { templateId = _template.Id })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _factory.CreateClient().GetAsync("/api/v1/tasks", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Another_organization_cannot_see_or_change_the_tasks()
    {
        var site = await _kit.AddSiteAsync("London");
        var task = await _kit.AddTaskAsync(site, _template, t => t.RequiresApproval = true);
        var photo = await _kit.AddAttachmentAsync(site);
        var intruder = _factory.CreateClient(await _factory.CreateOrganizationAsync("Other Org"));

        (await intruder.GetAsync(SiteTasks(site), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await intruder.GetAsync($"/api/v1/tasks/{task.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await (await intruder.GetAsync("/api/v1/tasks", Ct)).ReadAsync<PagedResult<TaskListItemResponse>>()).Items.ShouldBeEmpty();
        (await intruder.PutJsonAsync($"/api/v1/tasks/{task.Id}/progress", Answers())).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await intruder.PostJsonAsync($"/api/v1/tasks/{task.Id}/complete", Answers(("temp", Answer(3)), ("clean", Answer(true)))))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await intruder.PostJsonAsync($"/api/v1/tasks/{task.Id}/approve", new { })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await intruder.PostJsonAsync($"/api/v1/tasks/{task.Id}/snooze", new { date = TodayAt(site).AddDays(1) })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await intruder.PostJsonAsync(SiteTasks(site), new { templateId = _template.Id })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await intruder.GetAsync($"/api/v1/attachments/{photo.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _kit.ReloadAsync(task.Id)).Status.ShouldBe(SiteTaskStatus.Open);
    }
}
