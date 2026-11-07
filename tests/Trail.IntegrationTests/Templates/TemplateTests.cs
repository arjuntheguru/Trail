using System.Net;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Paging;
using Trail.Application.Common.Security;
using Trail.Application.Features.Schedules;
using Trail.Application.Features.Tags;
using Trail.Application.Features.Templates;
using Trail.Domain.Tasks;
using Trail.IntegrationTests.Infrastructure;

namespace Trail.IntegrationTests.Templates;

public sealed class TemplateTests(MySqlFixture mysql) : IAsyncLifetime
{
    private TrailApiFactory _factory = null!;
    private TestMember _owner = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = await TrailApiFactory.CreateAsync(mysql);
        _owner = await _factory.CreateOrganizationAsync();
        _client = _factory.CreateClient(_owner);
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static TemplateField Check(string key, string label = "Done?") =>
        new() { Key = key, Label = label, Type = FieldType.Checkbox };

    private static CreateTemplateCommand NewTemplate(
        string name, TaskKind kind = TaskKind.Checklist, TemplateField[]? fields = null, Guid[]? tagIds = null) =>
        new(name, "Description", kind, fields ?? [Check("a")], false, false, tagIds);

    private async Task<TemplateResponse> CreateAsync(CreateTemplateCommand command, HttpClient? client = null)
    {
        var response = await (client ?? _client).PostJsonAsync("/api/v1/templates", command);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return await response.ReadAsync<TemplateResponse>();
    }

    private static UpdateTemplateCommand Update(
        string name, TemplateField[] fields, Guid[]? tagIds = null, bool approval = false, bool photo = false) =>
        new(name, "Description", TaskKind.Checklist, fields, approval, photo, tagIds);

    private async Task<TagResponse> CreateTagAsync(string name)
    {
        var response = await _client.PostJsonAsync("/api/v1/tags", new CreateTagCommand(name, null));
        return await response.ReadAsync<TagResponse>();
    }

    private async Task<PagedResult<TemplateSummaryResponse>> ListAsync(string query = "") =>
        await (await _client.GetAsync($"/api/v1/templates{query}", Ct)).ReadAsync<PagedResult<TemplateSummaryResponse>>();

    [Fact]
    public async Task A_template_can_be_created_fetched_and_updated()
    {
        var tag = await CreateTagAsync("Opening");
        var created = await CreateAsync(NewTemplate("Opening checks", tagIds: [tag.Id]));

        created.Version.ShouldBe(1);
        created.IsActive.ShouldBeTrue();
        created.TagIds.ShouldBe([tag.Id]);

        var fetched = await (await _client.GetAsync($"/api/v1/templates/{created.Id}", Ct)).ReadAsync<TemplateResponse>();
        fetched.Name.ShouldBe("Opening checks");
        fetched.Fields.Select(f => f.Key).ShouldBe(["a"]);
        fetched.TagIds.ShouldBe([tag.Id]);

        var updated = await (await _client.PutJsonAsync($"/api/v1/templates/{created.Id}",
            Update("Opening checks v2", [Check("a"), Check("b")], tagIds: []))).ReadAsync<TemplateResponse>();
        updated.Name.ShouldBe("Opening checks v2");
        updated.Fields.Count.ShouldBe(2);
        updated.TagIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_list_filters_by_search_kind_tag_and_active()
    {
        var tag = await CreateTagAsync("Kitchen");
        await CreateAsync(NewTemplate("Fridge log", TaskKind.RecordLog, tagIds: [tag.Id]));
        await CreateAsync(NewTemplate("Opening checks"));
        var closing = await CreateAsync(NewTemplate("Closing checks"));
        (await _client.PostEmptyAsync($"/api/v1/templates/{closing.Id}/deactivate")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await ListAsync()).Items.Select(t => t.Name).ShouldBe(["Fridge log", "Opening checks"]);
        (await ListAsync("?includeInactive=true")).Items.Select(t => t.Name).ShouldBe(["Closing checks", "Fridge log", "Opening checks"]);
        (await ListAsync("?search=check")).Items.Select(t => t.Name).ShouldBe(["Opening checks"]);
        (await ListAsync("?kind=RecordLog")).Items.Select(t => t.Name).ShouldBe(["Fridge log"]);

        var byTag = (await ListAsync($"?tagId={tag.Id}")).Items.ShouldHaveSingleItem();
        byTag.Name.ShouldBe("Fridge log");
        byTag.Tags.Select(t => t.Name).ShouldBe(["Kitchen"]);
        byTag.FieldCount.ShouldBe(1);
        byTag.Version.ShouldBe(1);

        (await ListAsync("?pageSize=1")).TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task Invalid_fields_come_back_under_the_fields_key()
    {
        var response = await _client.PostJsonAsync("/api/v1/templates", NewTemplate("Bad", fields: [Check("a"), Check("a")]));

        await response.ShouldBeAsync(HttpStatusCode.BadRequest, "template.invalid_fields");
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("\"fields\"");

        (await _client.PostJsonAsync("/api/v1/templates", NewTemplate("Empty", fields: [])))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Names_and_lengths_are_validated()
    {
        (await _client.PostJsonAsync("/api/v1/templates", NewTemplate(""))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _client.PostJsonAsync("/api/v1/templates", NewTemplate(new string('x', TaskTemplate.NameMaxLength + 1))))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        await CreateAsync(NewTemplate("Same"));
        await (await _client.PostJsonAsync("/api/v1/templates", NewTemplate("Same")))
            .ShouldBeAsync(HttpStatusCode.Conflict, "template.name_taken");

        var other = await CreateAsync(NewTemplate("Other"));
        await (await _client.PutJsonAsync($"/api/v1/templates/{other.Id}", Update("Same", [Check("a")])))
            .ShouldBeAsync(HttpStatusCode.Conflict, "template.name_taken");
    }

    [Fact]
    public async Task Unknown_tags_are_rejected()
    {
        var response = await _client.PostJsonAsync("/api/v1/templates", NewTemplate("T", tagIds: [Guid.NewGuid()]));

        await response.ShouldBeAsync(HttpStatusCode.BadRequest, "template.unknown_tags");
    }

    [Fact]
    public async Task The_version_only_increases_when_the_fields_change()
    {
        var template = await CreateAsync(NewTemplate("Versioned"));
        var url = $"/api/v1/templates/{template.Id}";

        var renamed = await (await _client.PutJsonAsync(url, Update("Renamed", [Check("a")], approval: true))).ReadAsync<TemplateResponse>();
        renamed.Version.ShouldBe(1);

        var relabelled = await (await _client.PutJsonAsync(url, Update("Renamed", [Check("a", "New label")]))).ReadAsync<TemplateResponse>();
        relabelled.Version.ShouldBe(2);

        var same = await (await _client.PutJsonAsync(url, Update("Renamed", [Check("a", "New label")]))).ReadAsync<TemplateResponse>();
        same.Version.ShouldBe(2);
    }

    [Fact]
    public async Task Updating_refreshes_only_untouched_future_tasks()
    {
        var template = await CreateAsync(NewTemplate("Snapshot"));
        var site = await _client.CreateSiteAsync("Site");
        var future = DateTime.UtcNow.AddDays(2);

        async Task<SiteTask> AddTask(DateTime dueAt, Action<SiteTask>? configure = null)
        {
            var task = new SiteTask
            {
                Title = "Snapshot", Kind = TaskKind.Checklist, Fields = [Check("a")], SiteId = site.Id, TemplateId = template.Id,
                TemplateVersion = 1, Source = SiteTaskSource.AdHoc, DueAt = dueAt,
                ScheduledDate = DateOnly.FromDateTime(dueAt), DueDate = DateOnly.FromDateTime(dueAt),
            };
            configure?.Invoke(task);
            await _factory.WithDbAsync(_owner.OrganizationId, async db =>
            {
                db.SiteTasks.Add(task);
                await db.SaveChangesAsync();
                return 0;
            });
            return task;
        }

        var untouched = await AddTask(future);
        var started = await AddTask(future.AddDays(1), t => t.Entries.Add(new TaskEntry()));
        var past = await AddTask(DateTime.UtcNow.AddDays(-2));

        var updated = await (await _client.PutJsonAsync($"/api/v1/templates/{template.Id}",
            new UpdateTemplateCommand("Snapshot 2", "New description", TaskKind.RecordLog, [Check("b"), Check("c")], true, true, null)))
            .ReadAsync<TemplateResponse>();
        updated.Version.ShouldBe(2);

        var tasks = await _factory.WithDbAsync(_owner.OrganizationId, db => db.SiteTasks.AsNoTracking().ToDictionaryAsync(t => t.Id));

        var refreshed = tasks[untouched.Id];
        (refreshed.Title, refreshed.Description, refreshed.Kind, refreshed.TemplateVersion)
            .ShouldBe(("Snapshot 2", "New description", TaskKind.RecordLog, 2));
        refreshed.Fields.Select(f => f.Key).ShouldBe(["b", "c"]);
        (refreshed.RequiresApproval, refreshed.RequiresPhoto).ShouldBe((true, true));

        foreach (var kept in new[] { started, past })
        {
            tasks[kept.Id].Title.ShouldBe("Snapshot");
            tasks[kept.Id].TemplateVersion.ShouldBe(1);
            tasks[kept.Id].Fields.Select(f => f.Key).ShouldBe(["a"]);
        }
    }

    [Fact]
    public async Task A_template_can_be_deactivated_and_reactivated()
    {
        var template = await CreateAsync(NewTemplate("Toggle"));

        await _client.PostEmptyAsync($"/api/v1/templates/{template.Id}/deactivate");
        (await (await _client.GetAsync($"/api/v1/templates/{template.Id}", Ct)).ReadAsync<TemplateResponse>()).IsActive.ShouldBeFalse();

        await _client.PostEmptyAsync($"/api/v1/templates/{template.Id}/activate");
        (await (await _client.GetAsync($"/api/v1/templates/{template.Id}", Ct)).ReadAsync<TemplateResponse>()).IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Deleting_is_blocked_while_a_schedule_uses_the_template()
    {
        var template = await CreateAsync(NewTemplate("Used"));
        var site = await _client.CreateSiteAsync("Site");
        var schedule = await (await _client.PostJsonAsync("/api/v1/schedules", new CreateScheduleCommand(
            template.Id, Frequency.Daily, DateOnly.FromDateTime(DateTime.UtcNow), new TimeOnly(23, 0), SiteIds: [site.Id])))
            .ReadAsync<ScheduleResponse>();

        await (await _client.DeleteAsync($"/api/v1/templates/{template.Id}", Ct)).ShouldBeAsync(HttpStatusCode.Conflict, "template.in_use");

        (await _client.DeleteAsync($"/api/v1/schedules/{schedule.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _client.DeleteAsync($"/api/v1/templates/{template.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _client.GetAsync($"/api/v1/templates/{template.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ListAsync("?includeInactive=true")).Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_library_lists_shows_and_copies_templates()
    {
        var list = await (await _client.GetAsync("/api/v1/templates/library", Ct)).ReadAsync<List<LibraryTemplateSummaryResponse>>();
        list.Count.ShouldBe(TemplateLibrary.All.Count);
        var entry = list.First(t => t.Key == "opening-checks");
        entry.FieldCount.ShouldBe(TemplateLibrary.Find("opening-checks")!.Fields.Count);

        var detail = await (await _client.GetAsync("/api/v1/templates/library/opening-checks", Ct)).ReadAsync<LibraryTemplateResponse>();
        detail.Fields.Count.ShouldBe(entry.FieldCount);

        var first = await (await _client.PostEmptyAsync("/api/v1/templates/library/opening-checks/copy")).ReadAsync<TemplateResponse>();
        first.Name.ShouldBe(entry.Name);
        first.LibraryKey.ShouldBe("opening-checks");
        first.Fields.Count.ShouldBe(entry.FieldCount);
        (await _client.PostEmptyAsync("/api/v1/templates/library/opening-checks/copy")).StatusCode.ShouldBe(HttpStatusCode.Created);
        var third = await (await _client.PostEmptyAsync("/api/v1/templates/library/opening-checks/copy")).ReadAsync<TemplateResponse>();
        third.Name.ShouldBe($"{entry.Name} (3)");

        (await ListAsync("?search=" + Uri.EscapeDataString(entry.Name))).TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task An_unknown_library_key_is_not_found()
    {
        await (await _client.GetAsync("/api/v1/templates/library/nope", Ct)).ShouldBeAsync(HttpStatusCode.NotFound, "template.library_not_found");
        await (await _client.PostEmptyAsync("/api/v1/templates/library/nope/copy")).ShouldBeAsync(HttpStatusCode.NotFound, "template.library_not_found");
    }

    [Fact]
    public async Task Permissions_are_enforced()
    {
        var template = await CreateAsync(NewTemplate("Guarded"));
        var teamMember = _factory.CreateClient(await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.TeamMember));
        var siteManager = _factory.CreateClient(await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.SiteManager));

        // Team members cannot even read templates; site managers can read but not change them.
        (await teamMember.GetAsync("/api/v1/templates", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await teamMember.GetAsync("/api/v1/templates/library", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await siteManager.GetAsync($"/api/v1/templates/{template.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await siteManager.GetAsync("/api/v1/templates/library", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await siteManager.PostJsonAsync("/api/v1/templates", NewTemplate("Nope"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await siteManager.PutJsonAsync($"/api/v1/templates/{template.Id}", Update("Nope", [Check("a")]))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await siteManager.PostEmptyAsync($"/api/v1/templates/{template.Id}/deactivate")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await siteManager.DeleteAsync($"/api/v1/templates/{template.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await siteManager.PostEmptyAsync("/api/v1/templates/library/opening-checks/copy")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Another_organization_cannot_reach_a_template()
    {
        var template = await CreateAsync(NewTemplate("Private"));
        var other = _factory.CreateClient(await _factory.CreateOrganizationAsync("Other"));
        var url = $"/api/v1/templates/{template.Id}";

        (await other.GetAsync(url, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.PutJsonAsync(url, Update("Hijacked", [Check("a")]))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.PostEmptyAsync($"{url}/deactivate")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.DeleteAsync(url, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await (await other.GetAsync("/api/v1/templates?includeInactive=true", Ct)).ReadAsync<PagedResult<TemplateSummaryResponse>>())
            .Items.ShouldBeEmpty();

        // The same name is free in another organization, and the original is untouched.
        (await other.PostJsonAsync("/api/v1/templates", NewTemplate("Private"))).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await ListAsync()).Items.ShouldHaveSingleItem().Name.ShouldBe("Private");
    }
}
