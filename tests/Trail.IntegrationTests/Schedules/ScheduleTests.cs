using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Paging;
using Trail.Application.Common.Security;
using Trail.Application.Features.Schedules;
using Trail.Application.Features.Sites;
using Trail.Application.Features.Templates;
using Trail.Domain.Tasks;
using Trail.IntegrationTests.Infrastructure;

namespace Trail.IntegrationTests.Schedules;

public sealed class ScheduleTests(MySqlFixture mysql) : IAsyncLifetime
{
    // The generator uses the app's real clock, so dates are relative to the real today. A daily schedule starting
    // yesterday generates today and tomorrow for each site, and a 23:59 due time keeps both in the future.
    private static readonly DateOnly Yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
    private static readonly TimeOnly LateEvening = new(23, 59);

    private TrailApiFactory _factory = null!;
    private TestMember _owner = null!;
    private HttpClient _client = null!;
    private TemplateResponse _template = null!;
    private SiteResponse _siteA = null!;
    private SiteResponse _siteB = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = await TrailApiFactory.CreateAsync(mysql);
        _owner = await _factory.CreateOrganizationAsync();
        _client = _factory.CreateClient(_owner);
        _template = await CreateTemplateAsync(_client, "Opening checks");
        _siteA = await _client.CreateSiteAsync("Site A");
        _siteB = await _client.CreateSiteAsync("Site B");
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<TemplateResponse> CreateTemplateAsync(HttpClient client, string name)
    {
        var response = await client.PostJsonAsync("/api/v1/templates", new CreateTemplateCommand(
            name, null, TaskKind.Checklist, [new TemplateField { Key = "a", Label = "Done?", Type = FieldType.Checkbox }], false, false, null));
        return await response.ReadAsync<TemplateResponse>();
    }

    private CreateScheduleCommand Daily(
        TimeOnly? dueTime = null, Guid[]? siteIds = null, Guid[]? regionIds = null, bool allSites = false, Guid? templateId = null) =>
        new(templateId ?? _template.Id, Frequency.Daily, Yesterday, dueTime ?? LateEvening,
            AllSites: allSites, SiteIds: siteIds, RegionIds: regionIds);

    private static UpdateScheduleCommand AsUpdate(CreateScheduleCommand c) => new(
        c.TemplateId, c.Frequency, c.StartDate, c.DueTime, c.Interval, c.EndDate, c.Days, c.DayOfMonth, c.AvailableFrom,
        c.AllSites, c.SiteIds, c.RegionIds, c.DepartmentId);

    private async Task<ScheduleResponse> CreateAsync(CreateScheduleCommand command, HttpClient? client = null)
    {
        var response = await (client ?? _client).PostJsonAsync("/api/v1/schedules", command);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return await response.ReadAsync<ScheduleResponse>();
    }

    private Task<List<SiteTask>> TasksAsync(Guid scheduleId) =>
        _factory.WithDbAsync(_owner.OrganizationId, db =>
            db.SiteTasks.AsNoTracking().Where(t => t.ScheduleId == scheduleId).OrderBy(t => t.SiteId).ThenBy(t => t.ScheduledDate).ToListAsync());

    private Task AddEntryAsync(Guid taskId) =>
        _factory.WithDbAsync(_owner.OrganizationId, async db =>
        {
            db.TaskEntries.Add(new TaskEntry { SiteTaskId = taskId });
            await db.SaveChangesAsync();
            return 0;
        });

    private async Task<(TestMember Member, HttpClient Client)> ScopedManagerAsync(Guid[]? siteIds = null, Guid[]? regionIds = null)
    {
        var member = await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.SiteManager, allSites: false);
        await _client.SetAccessAsync(member.MembershipId, false, siteIds, regionIds);
        return (member, _factory.CreateClient(member));
    }

    private static async Task<List<ScheduleResponse>> ListAsync(HttpClient client, string query = "") =>
        (await (await client.GetAsync($"/api/v1/schedules{query}", Ct)).ReadAsync<PagedResult<ScheduleResponse>>()).Items.ToList();

    [Fact]
    public async Task Creating_a_schedule_generates_todays_and_tomorrows_tasks()
    {
        var schedule = await CreateAsync(Daily(siteIds: [_siteA.Id]));

        schedule.TemplateName.ShouldBe("Opening checks");
        schedule.Summary.ShouldBe("Every day");
        schedule.IsActive.ShouldBeTrue();
        schedule.SiteIds.ShouldBe([_siteA.Id]);

        var tasks = await TasksAsync(schedule.Id);
        tasks.Count.ShouldBe(2);
        tasks.ShouldAllBe(t => t.SiteId == _siteA.Id && t.Status == SiteTaskStatus.Open && t.TemplateId == _template.Id);

        var fetched = await (await _client.GetAsync($"/api/v1/schedules/{schedule.Id}", Ct)).ReadAsync<ScheduleResponse>();
        fetched.Id.ShouldBe(schedule.Id);
    }

    [Fact]
    public async Task A_schedule_for_all_sites_generates_for_every_site()
    {
        var schedule = await CreateAsync(Daily(allSites: true));

        (await TasksAsync(schedule.Id)).Select(t => t.SiteId).Distinct().Order().ShouldBe(new[] { _siteA.Id, _siteB.Id }.Order());
    }

    [Fact]
    public async Task Weekly_days_accept_enum_names_and_numbers()
    {
        object Body(object days) => new
        {
            templateId = _template.Id, frequency = "Weekly", interval = 2, startDate = Yesterday, days,
            dueTime = "17:30:00", siteIds = new[] { _siteA.Id },
        };

        var byName = await (await _client.PostAsJsonAsync("/api/v1/schedules", Body("Monday, Thursday"), HttpExtensions.Json, Ct))
            .ReadAsync<ScheduleResponse>();
        byName.Days.ShouldBe(WeekDays.Monday | WeekDays.Thursday);
        byName.Summary.ShouldBe("Every 2 weeks on Mon, Thu");
        byName.DueTime.ShouldBe(new TimeOnly(17, 30));

        var byNumber = await (await _client.PostAsJsonAsync("/api/v1/schedules", Body((int)(WeekDays.Tuesday | WeekDays.Friday)), HttpExtensions.Json, Ct))
            .ReadAsync<ScheduleResponse>();
        byNumber.Days.ShouldBe(WeekDays.Tuesday | WeekDays.Friday);
    }

    [Fact]
    public async Task Recurrence_problems_are_reported()
    {
        var site = new[] { _siteA.Id };

        async Task ShouldBeRejected(CreateScheduleCommand command, string? code = null) =>
            await (await _client.PostJsonAsync("/api/v1/schedules", command)).ShouldBeAsync(HttpStatusCode.BadRequest, code);

        await ShouldBeRejected(Daily(siteIds: site) with { Interval = 0 });
        await ShouldBeRejected(Daily(siteIds: site) with { EndDate = Yesterday.AddDays(-1) });
        await ShouldBeRejected(Daily(siteIds: site) with { Days = WeekDays.Monday });
        await ShouldBeRejected(Daily(siteIds: site) with { DayOfMonth = 5 });
        await ShouldBeRejected(Daily(siteIds: site) with { DueTime = null });

        var response = await _client.PostJsonAsync("/api/v1/schedules", Daily(siteIds: site) with { Interval = 0 });
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("recurrence");
    }

    [Fact]
    public async Task Available_from_must_be_earlier_than_the_due_time()
    {
        var site = new[] { _siteA.Id };

        await (await _client.PostJsonAsync("/api/v1/schedules",
            Daily(new TimeOnly(10, 0), siteIds: site) with { AvailableFrom = new TimeOnly(10, 0) }))
            .ShouldBeAsync(HttpStatusCode.BadRequest);
        await (await _client.PostJsonAsync("/api/v1/schedules",
            Daily(new TimeOnly(10, 0), siteIds: site) with { AvailableFrom = new TimeOnly(11, 0) }))
            .ShouldBeAsync(HttpStatusCode.BadRequest);

        var ok = await CreateAsync(Daily(new TimeOnly(23, 0), siteIds: site) with { AvailableFrom = new TimeOnly(6, 0) });
        ok.AvailableFrom.ShouldBe(new TimeOnly(6, 0));
    }

    [Fact]
    public async Task A_schedule_needs_a_target_and_known_ids_and_an_active_template()
    {
        await (await _client.PostJsonAsync("/api/v1/schedules", Daily()))
            .ShouldBeAsync(HttpStatusCode.BadRequest, "schedule.no_target");
        await (await _client.PostJsonAsync("/api/v1/schedules", Daily(siteIds: [Guid.NewGuid()])))
            .ShouldBeAsync(HttpStatusCode.BadRequest, "schedule.unknown_ids");
        await (await _client.PostJsonAsync("/api/v1/schedules", Daily(regionIds: [Guid.NewGuid()])))
            .ShouldBeAsync(HttpStatusCode.BadRequest, "schedule.unknown_ids");
        await (await _client.PostJsonAsync("/api/v1/schedules", Daily(siteIds: [_siteA.Id]) with { DepartmentId = Guid.NewGuid() }))
            .ShouldBeAsync(HttpStatusCode.BadRequest, "schedule.unknown_ids");
        await (await _client.PostJsonAsync("/api/v1/schedules", Daily(siteIds: [_siteA.Id], templateId: Guid.NewGuid())))
            .ShouldBeAsync(HttpStatusCode.BadRequest, "schedule.unknown_ids");

        await _client.PostEmptyAsync($"/api/v1/templates/{_template.Id}/deactivate");
        await (await _client.PostJsonAsync("/api/v1/schedules", Daily(siteIds: [_siteA.Id])))
            .ShouldBeAsync(HttpStatusCode.BadRequest, "schedule.template_inactive");
    }

    [Fact]
    public async Task Updating_regenerates_untouched_tasks_and_keeps_started_ones()
    {
        var schedule = await CreateAsync(Daily(new TimeOnly(23, 0), siteIds: [_siteA.Id]));
        var before = await TasksAsync(schedule.Id);
        var started = before[0];
        var untouched = before[1];
        await AddEntryAsync(started.Id);

        var response = await _client.PutJsonAsync($"/api/v1/schedules/{schedule.Id}", AsUpdate(Daily(new TimeOnly(22, 30), siteIds: [_siteA.Id])));
        (await response.ReadAsync<ScheduleResponse>()).DueTime.ShouldBe(new TimeOnly(22, 30));

        var after = await TasksAsync(schedule.Id);
        after.Count.ShouldBe(2);
        var keptTask = after.Single(t => t.Id == started.Id);
        keptTask.DueAt.ShouldBe(started.DueAt);
        after.ShouldNotContain(t => t.Id == untouched.Id);
        var regenerated = after.Single(t => t.Id != started.Id);
        regenerated.ScheduledDate.ShouldBe(untouched.ScheduledDate);
        regenerated.DueAt.ShouldBe(untouched.DueAt.AddMinutes(-30));
    }

    [Fact]
    public async Task Changing_the_targets_moves_the_untouched_tasks()
    {
        var schedule = await CreateAsync(Daily(siteIds: [_siteA.Id]));

        await _client.PutJsonAsync($"/api/v1/schedules/{schedule.Id}", AsUpdate(Daily(siteIds: [_siteB.Id])));

        (await TasksAsync(schedule.Id)).ShouldAllBe(t => t.SiteId == _siteB.Id);
        var fetched = await (await _client.GetAsync($"/api/v1/schedules/{schedule.Id}", Ct)).ReadAsync<ScheduleResponse>();
        fetched.SiteIds.ShouldBe([_siteB.Id]);
    }

    [Fact]
    public async Task Deactivating_removes_untouched_future_tasks_and_activating_brings_them_back()
    {
        var schedule = await CreateAsync(Daily(siteIds: [_siteA.Id]));
        var started = (await TasksAsync(schedule.Id))[0];
        await AddEntryAsync(started.Id);

        (await _client.PostEmptyAsync($"/api/v1/schedules/{schedule.Id}/deactivate")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await TasksAsync(schedule.Id)).ShouldHaveSingleItem().Id.ShouldBe(started.Id);
        (await ListAsync(_client)).ShouldBeEmpty();
        (await ListAsync(_client, "?includeInactive=true")).ShouldHaveSingleItem().IsActive.ShouldBeFalse();

        (await _client.PostEmptyAsync($"/api/v1/schedules/{schedule.Id}/activate")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await TasksAsync(schedule.Id)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Deleting_removes_untouched_future_tasks_and_hides_the_schedule()
    {
        var schedule = await CreateAsync(Daily(siteIds: [_siteA.Id]));
        var started = (await TasksAsync(schedule.Id))[0];
        await AddEntryAsync(started.Id);

        (await _client.DeleteAsync($"/api/v1/schedules/{schedule.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await TasksAsync(schedule.Id)).ShouldHaveSingleItem().Id.ShouldBe(started.Id);
        (await _client.GetAsync($"/api/v1/schedules/{schedule.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ListAsync(_client, "?includeInactive=true")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Snoozed_tasks_are_not_removed()
    {
        var schedule = await CreateAsync(Daily(siteIds: [_siteA.Id]));
        var snoozed = (await TasksAsync(schedule.Id))[1];
        await _factory.WithDbAsync(_owner.OrganizationId, async db =>
        {
            var task = await db.SiteTasks.SingleAsync(t => t.Id == snoozed.Id);
            task.SnoozeCount = 1;
            await db.SaveChangesAsync();
            return 0;
        });

        await _client.PostEmptyAsync($"/api/v1/schedules/{schedule.Id}/deactivate");

        (await TasksAsync(schedule.Id)).Select(t => t.Id).ShouldBe([snoozed.Id]);
    }

    [Fact]
    public async Task The_list_filters_and_describes_schedules()
    {
        var other = await CreateTemplateAsync(_client, "Closing checks");
        var onA = await CreateAsync(Daily(siteIds: [_siteA.Id]));
        var onB = await CreateAsync(Daily(siteIds: [_siteB.Id], templateId: other.Id) with { Frequency = Frequency.Weekly, Days = WeekDays.Weekdays });
        var everywhere = await CreateAsync(Daily(allSites: true));

        (await ListAsync(_client)).Select(s => s.Id).Order().ShouldBe(new[] { onA.Id, onB.Id, everywhere.Id }.Order());
        (await ListAsync(_client, $"?templateId={other.Id}")).ShouldHaveSingleItem().Id.ShouldBe(onB.Id);
        (await ListAsync(_client, $"?siteId={_siteA.Id}")).Select(s => s.Id).Order().ShouldBe(new[] { onA.Id, everywhere.Id }.Order());

        var item = (await ListAsync(_client, $"?templateId={other.Id}")).Single();
        item.TemplateName.ShouldBe("Closing checks");
        item.Summary.ShouldBe("Every week on Mon, Tue, Wed, Thu, Fri");
    }

    [Fact]
    public async Task Schedules_can_target_regions()
    {
        var region = await _client.CreateRegionAsync("North");
        var inRegion = await _client.CreateSiteAsync("In region", region.Id);

        var schedule = await CreateAsync(Daily(regionIds: [region.Id]));

        (await TasksAsync(schedule.Id)).ShouldAllBe(t => t.SiteId == inRegion.Id);
        (await ListAsync(_client, $"?siteId={inRegion.Id}")).ShouldHaveSingleItem().Id.ShouldBe(schedule.Id);
        (await ListAsync(_client, $"?siteId={_siteA.Id}")).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_scoped_manager_can_only_schedule_inside_their_scope()
    {
        var region = await _client.CreateRegionAsync("North");
        var inRegion = await _client.CreateSiteAsync("In region", region.Id);
        var (_, manager) = await ScopedManagerAsync(siteIds: [_siteA.Id]);

        (await CreateAsync(Daily(siteIds: [_siteA.Id]), manager)).SiteIds.ShouldBe([_siteA.Id]);

        await (await manager.PostJsonAsync("/api/v1/schedules", Daily(siteIds: [_siteB.Id])))
            .ShouldBeAsync(HttpStatusCode.Forbidden, "schedule.out_of_scope");
        await (await manager.PostJsonAsync("/api/v1/schedules", Daily(siteIds: [_siteA.Id, _siteB.Id])))
            .ShouldBeAsync(HttpStatusCode.Forbidden, "schedule.out_of_scope");
        await (await manager.PostJsonAsync("/api/v1/schedules", Daily(allSites: true)))
            .ShouldBeAsync(HttpStatusCode.Forbidden, "schedule.out_of_scope");
        await (await manager.PostJsonAsync("/api/v1/schedules", Daily(regionIds: [region.Id])))
            .ShouldBeAsync(HttpStatusCode.Forbidden, "schedule.out_of_scope");
        inRegion.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task A_manager_scoped_to_a_region_can_schedule_for_that_region()
    {
        var region = await _client.CreateRegionAsync("North");
        await _client.CreateSiteAsync("In region", region.Id);
        var (_, manager) = await ScopedManagerAsync(regionIds: [region.Id]);

        (await CreateAsync(Daily(regionIds: [region.Id]), manager)).RegionIds.ShouldBe([region.Id]);
    }

    [Fact]
    public async Task A_scoped_manager_cannot_see_edit_or_delete_schedules_reaching_outside_their_scope()
    {
        var (_, manager) = await ScopedManagerAsync(siteIds: [_siteA.Id]);
        var mine = await CreateAsync(Daily(siteIds: [_siteA.Id]));
        var both = await CreateAsync(Daily(siteIds: [_siteA.Id, _siteB.Id]));
        var everywhere = await CreateAsync(Daily(allSites: true));
        var elsewhere = await CreateAsync(Daily(siteIds: [_siteB.Id]));

        (await ListAsync(manager)).Select(s => s.Id).ShouldBe([mine.Id]);
        (await ListAsync(_client)).Count.ShouldBe(4);

        (await manager.GetAsync($"/api/v1/schedules/{mine.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        foreach (var hidden in new[] { both, everywhere, elsewhere })
        {
            var url = $"/api/v1/schedules/{hidden.Id}";
            await (await manager.GetAsync(url, Ct)).ShouldBeAsync(HttpStatusCode.NotFound, "schedule.not_found");
            await (await manager.PutJsonAsync(url, AsUpdate(Daily(siteIds: [_siteA.Id])))).ShouldBeAsync(HttpStatusCode.NotFound, "schedule.not_found");
            await (await manager.PostEmptyAsync($"{url}/deactivate")).ShouldBeAsync(HttpStatusCode.NotFound, "schedule.not_found");
            await (await manager.DeleteAsync(url, Ct)).ShouldBeAsync(HttpStatusCode.NotFound, "schedule.not_found");
        }

        // The hidden schedules are untouched.
        (await TasksAsync(everywhere.Id)).Count.ShouldBe(4);

        // Their own schedule cannot be stretched beyond their scope either.
        await (await manager.PutJsonAsync($"/api/v1/schedules/{mine.Id}", AsUpdate(Daily(siteIds: [_siteA.Id, _siteB.Id]))))
            .ShouldBeAsync(HttpStatusCode.Forbidden, "schedule.out_of_scope");
        (await manager.DeleteAsync($"/api/v1/schedules/{mine.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Team_members_cannot_manage_schedules()
    {
        var schedule = await CreateAsync(Daily(siteIds: [_siteA.Id]));
        var member = _factory.CreateClient(await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.TeamMember));
        var url = $"/api/v1/schedules/{schedule.Id}";

        (await member.GetAsync("/api/v1/schedules", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await member.GetAsync(url, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await member.PostJsonAsync("/api/v1/schedules", Daily(siteIds: [_siteA.Id]))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await member.PutJsonAsync(url, AsUpdate(Daily(siteIds: [_siteA.Id])))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await member.PostEmptyAsync($"{url}/deactivate")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await member.DeleteAsync(url, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Another_organization_cannot_reach_a_schedule()
    {
        var schedule = await CreateAsync(Daily(siteIds: [_siteA.Id]));
        var other = _factory.CreateClient(await _factory.CreateOrganizationAsync("Other"));
        var url = $"/api/v1/schedules/{schedule.Id}";

        (await other.GetAsync(url, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.PutJsonAsync(url, AsUpdate(Daily(siteIds: [_siteA.Id])))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.PostEmptyAsync($"{url}/deactivate")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.DeleteAsync(url, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ListAsync(other, "?includeInactive=true")).ShouldBeEmpty();

        // Nor can it schedule another organization's template or sites.
        await (await other.PostJsonAsync("/api/v1/schedules", Daily(siteIds: [_siteA.Id])))
            .ShouldBeAsync(HttpStatusCode.BadRequest, "schedule.unknown_ids");

        (await TasksAsync(schedule.Id)).Count.ShouldBe(2);
    }
}
