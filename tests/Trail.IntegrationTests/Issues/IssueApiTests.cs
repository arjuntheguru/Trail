using System.Net;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Paging;
using Trail.Application.Common.Security;
using Trail.Application.Features.Issues;
using Trail.Application.Features.Sites;
using Trail.Domain.Files;
using Trail.Domain.Identity;
using Trail.Domain.Issues;
using Trail.IntegrationTests.Infrastructure;

namespace Trail.IntegrationTests.Issues;

public sealed class IssueApiTests(MySqlFixture mysql) : IAsyncLifetime
{
    private TrailApiFactory _factory = null!;
    private TestMember _owner = null!;
    private HttpClient _ownerClient = null!;
    private SiteResponse _siteA = null!;
    private SiteResponse _siteB = null!;
    private Guid _org;

    public async ValueTask InitializeAsync()
    {
        _factory = await TrailApiFactory.CreateAsync(mysql);
        _owner = await _factory.CreateOrganizationAsync();
        _org = _owner.OrganizationId;
        _ownerClient = _factory.CreateClient(_owner);
        _siteA = await _ownerClient.CreateSiteAsync("Site A");
        _siteB = await _ownerClient.CreateSiteAsync("Site B");
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Url(Guid id, string? action = null) => $"/api/v1/issues/{id}" + (action is null ? "" : $"/{action}");

    private async Task<(TestMember Member, HttpClient Client)> MemberAsync(string role, params Guid[] siteIds)
    {
        var member = await _factory.AddMemberAsync(_org, role, allSites: siteIds.Length == 0);
        if (siteIds.Length > 0) await _ownerClient.SetAccessAsync(member.MembershipId, false, siteIds);
        return (member, _factory.CreateClient(member));
    }

    private static Task<HttpResponseMessage> RaiseRawAsync(
        HttpClient client, Guid siteId, string title = "Fridge is warm", IssuePriority? priority = null,
        Guid? assigneeId = null, DateTime? dueAt = null, Guid[]? photoIds = null) =>
        client.PostJsonAsync("/api/v1/issues",
            new RaiseIssueCommand(title, "Description", siteId, null, priority, dueAt, assigneeId, photoIds));

    private static async Task<IssueDetailResponse> RaiseAsync(
        HttpClient client, Guid siteId, string title = "Fridge is warm", IssuePriority? priority = null,
        Guid? assigneeId = null, DateTime? dueAt = null)
    {
        var response = await RaiseRawAsync(client, siteId, title, priority, assigneeId, dueAt);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return await response.ReadAsync<IssueDetailResponse>();
    }

    private async Task<Guid> AddAttachmentAsync(Guid? siteId, AttachmentKind kind = AttachmentKind.Photo)
    {
        var id = Guid.CreateVersion7();
        await _factory.WithDbAsync(_org, async db =>
        {
            db.Attachments.Add(new Attachment
            {
                Kind = kind,
                FileName = "photo.jpg",
                ContentType = "image/jpeg",
                SizeBytes = 10,
                StorageKey = $"test/{id}",
                Sha256 = new string('a', 64),
                SiteId = siteId,
            });
            return await db.SaveChangesAsync();
        });
        // The id is assigned by the entity; read it back.
        return await _factory.WithDbAsync(_org, db =>
            db.Attachments.Where(a => a.StorageKey == $"test/{id}").Select(a => a.Id).SingleAsync());
    }

    private static async Task Expect(Task<HttpResponseMessage> call, HttpStatusCode status, string? code = null) =>
        await (await call).ShouldBeAsync(status, code);

    private Task<int> SetDueAsync(Guid id, DateTime? dueAt) =>
        _factory.WithDbAsync(_org, db => db.Issues.Where(i => i.Id == id).ExecuteUpdateAsync(s => s.SetProperty(i => i.DueAt, dueAt)));

    private Task<PagedResult<IssueListItemResponse>> ListAsync(HttpClient client, string query = "") =>
        ReadListAsync(client, query);

    private static async Task<PagedResult<IssueListItemResponse>> ReadListAsync(HttpClient client, string query) =>
        await (await client.GetAsync($"/api/v1/issues?pageSize=100&{query}", Ct)).ReadAsync<PagedResult<IssueListItemResponse>>();

    // ---- Raising ----

    [Fact]
    public async Task Raising_numbers_issues_sequentially_and_defaults_the_due_time_by_priority()
    {
        var before = DateTime.UtcNow;
        var first = await RaiseAsync(_ownerClient, _siteA.Id, priority: IssuePriority.Critical);
        var second = await RaiseAsync(_ownerClient, _siteA.Id, priority: IssuePriority.High);
        var third = await RaiseAsync(_ownerClient, _siteB.Id);
        var fourth = await RaiseAsync(_ownerClient, _siteB.Id, priority: IssuePriority.Low);

        new[] { first.Number, second.Number, third.Number, fourth.Number }.ShouldBe([1, 2, 3, 4]);
        first.DueAt!.Value.ShouldBe(before.AddHours(4), TimeSpan.FromMinutes(1));
        second.DueAt!.Value.ShouldBe(before.AddHours(24), TimeSpan.FromMinutes(1));
        third.Priority.ShouldBe(IssuePriority.Medium);
        third.DueAt!.Value.ShouldBe(before.AddDays(3), TimeSpan.FromMinutes(1));
        fourth.DueAt!.Value.ShouldBe(before.AddDays(7), TimeSpan.FromMinutes(1));

        first.Source.ShouldBe(IssueSource.Manual);
        first.Status.ShouldBe(IssueStatus.Open);
        first.SiteName.ShouldBe("Site A");
        first.IsOverdue.ShouldBeFalse();
        var raised = first.Timeline.ShouldHaveSingleItem();
        raised.Kind.ShouldBe(IssueEventKind.Raised);
        raised.To.ShouldBe("Critical");
        raised.ActorId.ShouldBe(_owner.UserId);
        raised.ActorName.ShouldBe("Test User");

        var fetched = await (await _ownerClient.GetAsync(Url(first.Id), Ct)).ReadAsync<IssueDetailResponse>();
        fetched.Number.ShouldBe(1);
        fetched.Description.ShouldBe("Description");
    }

    [Fact]
    public async Task Raising_with_an_explicit_due_time_and_department_keeps_them()
    {
        var department = await _ownerClient.CreateDepartmentAsync("Kitchen");
        var due = DateTime.UtcNow.AddDays(10);
        var response = await _ownerClient.PostJsonAsync("/api/v1/issues",
            new RaiseIssueCommand("Pest sighting", null, _siteA.Id, department.Id, IssuePriority.High, due, null, null));

        var issue = await response.ReadAsync<IssueDetailResponse>();
        issue.DueAt!.Value.ShouldBe(due, TimeSpan.FromSeconds(1));
        issue.DepartmentName.ShouldBe("Kitchen");

        await Expect(_ownerClient.PostJsonAsync("/api/v1/issues",
            new RaiseIssueCommand("x", null, _siteA.Id, Guid.NewGuid(), null, null, null, null)), HttpStatusCode.BadRequest, "issue.unknown_department");
    }

    [Fact]
    public async Task Raising_with_an_assignee_records_raised_then_assigned()
    {
        var (manager, _) = await MemberAsync(SystemRoles.SiteManager);

        var issue = await RaiseAsync(_ownerClient, _siteA.Id, assigneeId: manager.MembershipId);

        issue.Assignee.ShouldNotBeNull().MembershipId.ShouldBe(manager.MembershipId);
        issue.Assignee!.Name.ShouldBe("Test User");
        issue.Timeline.Select(e => e.Kind).ShouldBe([IssueEventKind.Raised, IssueEventKind.Assigned]);
        issue.Timeline[1].ToName.ShouldBe("Test User");
        issue.Timeline[1].FromName.ShouldBeNull();
    }

    [Fact]
    public async Task Raising_validates_its_input()
    {
        await Expect(RaiseRawAsync(_ownerClient, _siteA.Id, dueAt: DateTime.UtcNow.AddMinutes(-5)), HttpStatusCode.BadRequest, "issue.due_in_past");
        (await RaiseRawAsync(_ownerClient, _siteA.Id, title: "")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await RaiseRawAsync(_ownerClient, _siteA.Id, title: new string('x', Issue.TitleMaxLength + 1)))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await Expect(RaiseRawAsync(_ownerClient, Guid.NewGuid()), HttpStatusCode.BadRequest, "issue.unknown_site");

        var response = await _ownerClient.PostJsonAsync("/api/v1/issues", new RaiseIssueCommand(
            "Long", new string('d', Issue.DescriptionMaxLength + 1), _siteA.Id, null, null, null, null, null));
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ListAsync(_ownerClient)).TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Raising_on_a_site_outside_the_callers_scope_or_an_inactive_site_is_rejected()
    {
        var (_, scoped) = await MemberAsync(SystemRoles.TeamMember, _siteA.Id);
        await Expect(RaiseRawAsync(scoped, _siteB.Id), HttpStatusCode.BadRequest, "issue.unknown_site");
        (await RaiseRawAsync(scoped, _siteA.Id)).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await _ownerClient.PostEmptyAsync($"/api/v1/sites/{_siteB.Id}/deactivate")).EnsureSuccessStatusCode();
        await Expect(RaiseRawAsync(_ownerClient, _siteB.Id), HttpStatusCode.BadRequest, "issue.site_inactive");
    }

    [Fact]
    public async Task Photos_must_be_photos_of_the_issues_site_in_this_organization()
    {
        var good = await AddAttachmentAsync(_siteA.Id);
        var otherSite = await AddAttachmentAsync(_siteB.Id);
        var noSite = await AddAttachmentAsync(null);
        var document = await AddAttachmentAsync(_siteA.Id, AttachmentKind.Document);

        foreach (var bad in new[] { otherSite, noSite, document, Guid.NewGuid() })
            await Expect(RaiseRawAsync(_ownerClient, _siteA.Id, photoIds: [good, bad]), HttpStatusCode.BadRequest, "issue.unknown_attachments");

        var tooMany = Enumerable.Range(0, Issue.MaxPhotos + 1).Select(_ => Guid.NewGuid()).ToArray();
        await Expect(RaiseRawAsync(_ownerClient, _siteA.Id, photoIds: tooMany), HttpStatusCode.BadRequest, "issue.unknown_attachments");

        // A photo from another organization looks unknown too.
        var other = await _factory.CreateOrganizationAsync("Other");
        var foreignSite = await _factory.CreateClient(other).CreateSiteAsync("Foreign");
        await _factory.WithDbAsync(other.OrganizationId, async db =>
        {
            db.Attachments.Add(new Attachment
            {
                Kind = AttachmentKind.Photo, FileName = "f.jpg", ContentType = "image/jpeg", SizeBytes = 1,
                StorageKey = "foreign", Sha256 = new string('b', 64), SiteId = foreignSite.Id,
            });
            return await db.SaveChangesAsync();
        });
        var foreign = await _factory.WithDbAsync(other.OrganizationId, db => db.Attachments.Select(a => a.Id).SingleAsync());
        await Expect(RaiseRawAsync(_ownerClient, _siteA.Id, photoIds: [foreign]), HttpStatusCode.BadRequest, "issue.unknown_attachments");

        var response = await RaiseRawAsync(_ownerClient, _siteA.Id, photoIds: [good, good]);
        var issue = await response.ReadAsync<IssueDetailResponse>();
        issue.PhotoIds.ShouldBe([good]);
    }

    [Fact]
    public async Task Assignees_must_be_active_members_with_issue_access_to_the_site()
    {
        var (anyone, _) = await MemberAsync(SystemRoles.TeamMember);
        var (siteBOnly, _) = await MemberAsync(SystemRoles.TeamMember, _siteB.Id);
        var (siteAOnly, _) = await MemberAsync(SystemRoles.TeamMember, _siteA.Id);

        var region = await _ownerClient.CreateRegionAsync("North");
        var regional = await _factory.AddMemberAsync(_org, SystemRoles.TeamMember, allSites: false);
        await _ownerClient.SetAccessAsync(regional.MembershipId, false, regionIds: [region.Id]);
        var regionalSite = await _ownerClient.CreateSiteAsync("Regional site", region.Id);

        await _factory.WithDbAsync(_org, async db =>
        {
            db.Roles.Add(new Role { Name = "Sites only", Permissions = [Permissions.Sites.View] });
            return await db.SaveChangesAsync();
        });
        var noIssues = await _factory.AddMemberAsync(_org, "Sites only");
        var suspended = await _factory.AddMemberAsync(_org);
        await _factory.WithDbAsync(_org, db => db.Memberships.Where(m => m.Id == suspended.MembershipId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, MembershipStatus.Suspended)));

        await Expect(RaiseRawAsync(_ownerClient, _siteA.Id, assigneeId: siteBOnly.MembershipId), HttpStatusCode.BadRequest, "issue.invalid_assignee");
        await Expect(RaiseRawAsync(_ownerClient, _siteA.Id, assigneeId: noIssues.MembershipId), HttpStatusCode.BadRequest, "issue.invalid_assignee");
        await Expect(RaiseRawAsync(_ownerClient, _siteA.Id, assigneeId: suspended.MembershipId), HttpStatusCode.BadRequest, "issue.invalid_assignee");
        await Expect(RaiseRawAsync(_ownerClient, _siteA.Id, assigneeId: Guid.NewGuid()), HttpStatusCode.BadRequest, "issue.invalid_assignee");

        // Reaching the site through all sites, a direct assignment or a region all qualify.
        (await RaiseRawAsync(_ownerClient, _siteA.Id, assigneeId: anyone.MembershipId)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await RaiseRawAsync(_ownerClient, _siteA.Id, assigneeId: siteAOnly.MembershipId)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await RaiseRawAsync(_ownerClient, regionalSite.Id, assigneeId: regional.MembershipId)).StatusCode.ShouldBe(HttpStatusCode.Created);

        // The same rules apply when assigning later.
        var issue = await RaiseAsync(_ownerClient, _siteA.Id);
        await Expect(_ownerClient.PutJsonAsync(Url(issue.Id, "assignee"), new AssignIssueCommand(siteBOnly.MembershipId)), HttpStatusCode.BadRequest, "issue.invalid_assignee");
    }

    // ---- Lifecycle ----

    [Fact]
    public async Task An_issue_moves_through_its_whole_lifecycle_and_the_timeline_records_it_in_order()
    {
        var (assignee, assigneeClient) = await MemberAsync(SystemRoles.SiteManager);
        var photo = await AddAttachmentAsync(_siteA.Id);

        var issue = await RaiseAsync(_ownerClient, _siteA.Id);
        var assigned = await (await _ownerClient.PutJsonAsync(Url(issue.Id, "assignee"), new AssignIssueCommand(assignee.MembershipId)))
            .ReadAsync<IssueDetailResponse>();
        assigned.Assignee!.MembershipId.ShouldBe(assignee.MembershipId);

        (await _ownerClient.PutJsonAsync(Url(issue.Id, "priority"), new ChangeIssuePriorityCommand(IssuePriority.Critical)))
            .EnsureSuccessStatusCode();
        var newDue = DateTime.UtcNow.AddDays(2);
        var due = await (await _ownerClient.PutJsonAsync(Url(issue.Id, "due"), new SetIssueDueCommand(newDue))).ReadAsync<IssueDetailResponse>();
        due.DueAt!.Value.ShouldBe(newDue, TimeSpan.FromSeconds(1));

        var started = await (await assigneeClient.PostEmptyAsync(Url(issue.Id, "start"))).ReadAsync<IssueDetailResponse>();
        started.Status.ShouldBe(IssueStatus.InProgress);

        var resolved = await (await assigneeClient.PostJsonAsync(Url(issue.Id, "resolve"),
            new ResolveIssueCommand("Engineer replaced the thermostat.", [photo]))).ReadAsync<IssueDetailResponse>();
        resolved.Status.ShouldBe(IssueStatus.Resolved);
        resolved.Resolution.ShouldBe("Engineer replaced the thermostat.");
        resolved.ResolvedAt.ShouldNotBeNull();
        resolved.ResolvedBy.ShouldBe("Test User");

        var closed = await (await _ownerClient.PostJsonAsync(Url(issue.Id, "close"), new CloseIssueCommand("Verified."))).ReadAsync<IssueDetailResponse>();
        closed.Status.ShouldBe(IssueStatus.Closed);
        closed.ClosedBy.ShouldBe("Test User");

        var reopened = await (await _ownerClient.PostJsonAsync(Url(issue.Id, "reopen"), new ReopenIssueCommand("Fault is back.")))
            .ReadAsync<IssueDetailResponse>();
        reopened.Status.ShouldBe(IssueStatus.Open);
        reopened.Resolution.ShouldBeNull();
        reopened.ResolvedAt.ShouldBeNull();
        reopened.ClosedAt.ShouldBeNull();

        var commented = await (await assigneeClient.PostJsonAsync(Url(issue.Id, "comments"),
            new CommentOnIssueCommand("Parts ordered.", null))).ReadAsync<IssueDetailResponse>();

        commented.Timeline.Select(e => (e.Kind, e.From, e.To)).ShouldBe(
        [
            (IssueEventKind.Raised, null, "Medium"),
            (IssueEventKind.Assigned, null, assignee.MembershipId.ToString()),
            (IssueEventKind.PriorityChanged, "Medium", "Critical"),
            (IssueEventKind.DueChanged, issue.DueAt!.Value.ToString("O"), newDue.ToString("O")),
            (IssueEventKind.StatusChanged, "Open", "InProgress"),
            (IssueEventKind.StatusChanged, "InProgress", "Resolved"),
            (IssueEventKind.StatusChanged, "Resolved", "Closed"),
            (IssueEventKind.StatusChanged, "Closed", "Open"),
            (IssueEventKind.Comment, null, null),
        ], ignoreOrder: false);
        commented.Timeline[1].ToName.ShouldBe("Test User");
        commented.Timeline[5].PhotoIds.ShouldBe([photo]);
        commented.Timeline[5].Note.ShouldBe("Engineer replaced the thermostat.");
        commented.Timeline[5].ActorId.ShouldBe(assignee.UserId);
        commented.Timeline[8].Note.ShouldBe("Parts ordered.");
        commented.Timeline.Select(e => e.OccurredAt).ShouldBeInOrder();
    }

    [Fact]
    public async Task Reassigning_names_the_previous_and_new_assignee_and_unassigning_is_allowed()
    {
        var (first, _) = await MemberAsync(SystemRoles.SiteManager);
        var (second, _) = await MemberAsync(SystemRoles.SiteManager);
        var issue = await RaiseAsync(_ownerClient, _siteA.Id, assigneeId: first.MembershipId);

        await (await _ownerClient.PutJsonAsync(Url(issue.Id, "assignee"), new AssignIssueCommand(second.MembershipId))).ReadAsync<IssueDetailResponse>();
        var cleared = await (await _ownerClient.PutJsonAsync(Url(issue.Id, "assignee"), new AssignIssueCommand(null))).ReadAsync<IssueDetailResponse>();

        cleared.Assignee.ShouldBeNull();
        var changes = cleared.Timeline.Where(e => e.Kind == IssueEventKind.Assigned).ToList();
        changes.Count.ShouldBe(3);
        changes[1].From.ShouldBe(first.MembershipId.ToString());
        changes[1].To.ShouldBe(second.MembershipId.ToString());
        changes[1].FromName.ShouldBe("Test User");
        changes[1].ToName.ShouldBe("Test User");
        changes[2].To.ShouldBeNull();
        changes[2].ToName.ShouldBeNull();
    }

    [Fact]
    public async Task Start_and_resolve_need_the_assignee_or_a_manager()
    {
        var (assignee, assigneeClient) = await MemberAsync(SystemRoles.TeamMember);
        var (_, bystander) = await MemberAsync(SystemRoles.TeamMember);
        var issue = await RaiseAsync(_ownerClient, _siteA.Id, assigneeId: assignee.MembershipId);

        await Expect(bystander.PostEmptyAsync(Url(issue.Id, "start")), HttpStatusCode.Forbidden, "issue.not_assignee");
        await Expect(bystander.PostJsonAsync(Url(issue.Id, "resolve"), new ResolveIssueCommand("Done", null)), HttpStatusCode.Forbidden, "issue.not_assignee");

        (await assigneeClient.PostEmptyAsync(Url(issue.Id, "start"))).StatusCode.ShouldBe(HttpStatusCode.OK);

        // A manager who is not the assignee may resolve.
        (await _ownerClient.PostJsonAsync(Url(issue.Id, "resolve"), new ResolveIssueCommand("Manager fixed it", null)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Actions_in_the_wrong_state_return_conflicts_with_stable_codes()
    {
        var (assignee, _) = await MemberAsync(SystemRoles.SiteManager);
        var issue = await RaiseAsync(_ownerClient, _siteA.Id);

        await Expect(_ownerClient.PostJsonAsync(Url(issue.Id, "reopen"), new ReopenIssueCommand("why")), HttpStatusCode.Conflict, "issue.not_resolved_or_closed");

        (await _ownerClient.PostEmptyAsync(Url(issue.Id, "start"))).EnsureSuccessStatusCode();
        await Expect(_ownerClient.PostEmptyAsync(Url(issue.Id, "start")), HttpStatusCode.Conflict, "issue.not_open");

        (await _ownerClient.PostJsonAsync(Url(issue.Id, "resolve"), new ResolveIssueCommand("Fixed", null))).EnsureSuccessStatusCode();
        await Expect(_ownerClient.PostEmptyAsync(Url(issue.Id, "start")), HttpStatusCode.Conflict, "issue.not_open");
        await Expect(_ownerClient.PostJsonAsync(Url(issue.Id, "resolve"), new ResolveIssueCommand("Again", null)), HttpStatusCode.Conflict, "issue.not_active");
        await Expect(_ownerClient.PutJsonAsync(Url(issue.Id, "assignee"), new AssignIssueCommand(assignee.MembershipId)), HttpStatusCode.Conflict, "issue.not_active");
        await Expect(_ownerClient.PutJsonAsync(Url(issue.Id, "priority"), new ChangeIssuePriorityCommand(IssuePriority.Low)), HttpStatusCode.Conflict, "issue.not_active");
        await Expect(_ownerClient.PutJsonAsync(Url(issue.Id, "due"), new SetIssueDueCommand(DateTime.UtcNow.AddDays(1))), HttpStatusCode.Conflict, "issue.not_active");

        // Resolved issues can be closed (no body at all is fine) and then not closed again.
        (await _ownerClient.PostEmptyAsync(Url(issue.Id, "close"))).StatusCode.ShouldBe(HttpStatusCode.OK);
        await Expect(_ownerClient.PostJsonAsync(Url(issue.Id, "close"), new CloseIssueCommand(null)), HttpStatusCode.Conflict, "issue.already_closed");

        // Comments stay possible on closed issues.
        (await _ownerClient.PostJsonAsync(Url(issue.Id, "comments"), new CommentOnIssueCommand("Late note", null)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Transition_bodies_are_validated()
    {
        var issue = await RaiseAsync(_ownerClient, _siteA.Id);
        var tooLong = new string('x', Issue.NoteMaxLength + 1);

        (await _ownerClient.PostJsonAsync(Url(issue.Id, "resolve"), new ResolveIssueCommand("", null))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _ownerClient.PostJsonAsync(Url(issue.Id, "resolve"), new ResolveIssueCommand(tooLong, null))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _ownerClient.PostJsonAsync(Url(issue.Id, "comments"), new CommentOnIssueCommand(null, null))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _ownerClient.PostJsonAsync(Url(issue.Id, "comments"), new CommentOnIssueCommand(tooLong, null))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _ownerClient.PostJsonAsync(Url(issue.Id, "close"), new CloseIssueCommand(tooLong))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await Expect(_ownerClient.PutJsonAsync(Url(issue.Id, "due"), new SetIssueDueCommand(DateTime.UtcNow.AddDays(-1))), HttpStatusCode.BadRequest, "issue.due_in_past");

        (await _ownerClient.PostJsonAsync(Url(issue.Id, "resolve"), new ResolveIssueCommand("ok", null))).EnsureSuccessStatusCode();
        (await _ownerClient.PostJsonAsync(Url(issue.Id, "reopen"), new ReopenIssueCommand(" "))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var cleared = await (await _ownerClient.PostJsonAsync(Url(issue.Id, "reopen"), new ReopenIssueCommand("Still broken"))).ReadAsync<IssueDetailResponse>();
        var undated = await (await _ownerClient.PutJsonAsync(Url(issue.Id, "due"), new SetIssueDueCommand(null))).ReadAsync<IssueDetailResponse>();
        undated.DueAt.ShouldBeNull();
        cleared.Status.ShouldBe(IssueStatus.Open);
    }

    [Fact]
    public async Task Unknown_issue_ids_return_404()
    {
        var id = Guid.NewGuid();
        await Expect(_ownerClient.GetAsync(Url(id), Ct), HttpStatusCode.NotFound, "issue.not_found");
        await Expect(_ownerClient.PostEmptyAsync(Url(id, "start")), HttpStatusCode.NotFound, "issue.not_found");
        await Expect(_ownerClient.PostJsonAsync(Url(id, "comments"), new CommentOnIssueCommand("x", null)), HttpStatusCode.NotFound, "issue.not_found");
    }

    // ---- Listing ----

    [Fact]
    public async Task The_list_orders_active_issues_first_then_by_priority_due_time_and_number()
    {
        var soon = DateTime.UtcNow.AddDays(1);
        var low = await RaiseAsync(_ownerClient, _siteA.Id, "low", IssuePriority.Low, dueAt: soon);                              // 1
        var critical = await RaiseAsync(_ownerClient, _siteA.Id, "critical", IssuePriority.Critical, dueAt: soon.AddDays(1));    // 2
        var undated = await RaiseAsync(_ownerClient, _siteA.Id, "undated", IssuePriority.High);                                  // 3
        var dueSoon = await RaiseAsync(_ownerClient, _siteA.Id, "due soon", IssuePriority.High, dueAt: soon);                    // 4
        var closed = await RaiseAsync(_ownerClient, _siteA.Id, "closed", IssuePriority.Critical);                                // 5
        var dueLater = await RaiseAsync(_ownerClient, _siteA.Id, "due later", IssuePriority.High, dueAt: soon.AddDays(2));        // 6
        await SetDueAsync(undated.Id, null);
        (await _ownerClient.PostEmptyAsync(Url(closed.Id, "close"))).EnsureSuccessStatusCode();

        var list = await ListAsync(_ownerClient);

        list.Items.Select(i => i.Number).ShouldBe([critical.Number, dueSoon.Number, dueLater.Number, undated.Number, low.Number, closed.Number]);
        list.TotalCount.ShouldBe(6);
        list.Items[0].SiteName.ShouldBe("Site A");
        list.Items[0].PhotoCount.ShouldBe(0);
    }

    [Fact]
    public async Task The_list_filters_by_status_priority_assignee_source_overdue_search_and_dates()
    {
        var (assignee, assigneeClient) = await MemberAsync(SystemRoles.SiteManager);
        var photo = await AddAttachmentAsync(_siteA.Id);
        var open = await RaiseAsync(_ownerClient, _siteA.Id, "Walk-in freezer door", IssuePriority.High, assigneeId: assignee.MembershipId);
        var started = await RaiseAsync(_ownerClient, _siteA.Id, "Hand wash sink blocked");
        var resolved = await RaiseAsync(_ownerClient, _siteB.Id, "Pest droppings");
        var overdue = await RaiseAsync(_ownerClient, _siteB.Id, "Late repair");
        (await _ownerClient.PostEmptyAsync(Url(started.Id, "start"))).EnsureSuccessStatusCode();
        (await _ownerClient.PostJsonAsync(Url(resolved.Id, "resolve"), new ResolveIssueCommand("Cleaned", null))).EnsureSuccessStatusCode();
        await SetDueAsync(overdue.Id, DateTime.UtcNow.AddHours(-3));
        // A resolved issue past its due time is not overdue.
        await SetDueAsync(resolved.Id, DateTime.UtcNow.AddHours(-3));
        await _factory.WithDbAsync(_org, db => db.Issues.Where(i => i.Id == started.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Source, IssueSource.TaskFailure)));
        (await _ownerClient.PostJsonAsync(Url(open.Id, "comments"), new CommentOnIssueCommand("Photo", [photo]))).EnsureSuccessStatusCode();

        static int[] Numbers(PagedResult<IssueListItemResponse> page) => [.. page.Items.Select(i => i.Number).Order()];

        Numbers(await ListAsync(_ownerClient, "status=Open")).ShouldBe([open.Number, overdue.Number]);
        Numbers(await ListAsync(_ownerClient, "status=Open&status=InProgress")).ShouldBe([open.Number, started.Number, overdue.Number]);
        Numbers(await ListAsync(_ownerClient, "status=Resolved")).ShouldBe([resolved.Number]);
        Numbers(await ListAsync(_ownerClient, "priority=High")).ShouldBe([open.Number]);
        Numbers(await ListAsync(_ownerClient, $"assigneeId={assignee.MembershipId}")).ShouldBe([open.Number]);
        Numbers(await ListAsync(_ownerClient, $"siteId={_siteB.Id}")).ShouldBe([resolved.Number, overdue.Number]);
        Numbers(await ListAsync(_ownerClient, "source=TaskFailure")).ShouldBe([started.Number]);
        Numbers(await ListAsync(_ownerClient, "overdue=true")).ShouldBe([overdue.Number]);
        (await ListAsync(_ownerClient, "overdue=true")).Items.Single().IsOverdue.ShouldBeTrue();
        Numbers(await ListAsync(_ownerClient, "search=freezer")).ShouldBe([open.Number]);
        Numbers(await ListAsync(_ownerClient, $"search=%23{resolved.Number}")).ShouldBe([resolved.Number]);
        Numbers(await ListAsync(_ownerClient, $"search={overdue.Number}")).ShouldBe([overdue.Number]);
        Numbers(await ListAsync(_ownerClient, $"from={Uri.EscapeDataString(DateTime.UtcNow.AddHours(1).ToString("O"))}")).ShouldBeEmpty();
        Numbers(await ListAsync(_ownerClient, $"to={Uri.EscapeDataString(DateTime.UtcNow.AddHours(1).ToString("O"))}")).Length.ShouldBe(4);

        var mine = await ListAsync(assigneeClient, "mine=true");
        mine.Items.ShouldHaveSingleItem().Number.ShouldBe(open.Number);
        mine.Items[0].Assignee!.MembershipId.ShouldBe(assignee.MembershipId);
        mine.Items[0].Assignee!.Name.ShouldBe("Test User");
        (await ListAsync(_ownerClient, "mine=true")).TotalCount.ShouldBe(0);

        var secondPage = await (await _ownerClient.GetAsync("/api/v1/issues?pageSize=2&page=2", Ct)).ReadAsync<PagedResult<IssueListItemResponse>>();
        secondPage.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task The_list_counts_photos()
    {
        var photo = await AddAttachmentAsync(_siteA.Id);
        var response = await RaiseRawAsync(_ownerClient, _siteA.Id, photoIds: [photo]);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        (await ListAsync(_ownerClient)).Items.Single().PhotoCount.ShouldBe(1);
    }

    [Fact]
    public async Task Members_only_see_and_reach_issues_at_their_own_sites()
    {
        var onA = await RaiseAsync(_ownerClient, _siteA.Id, "On A");
        var onB = await RaiseAsync(_ownerClient, _siteB.Id, "On B");
        var (_, scoped) = await MemberAsync(SystemRoles.SiteManager, _siteA.Id);
        var nothing = _factory.CreateClient(await _factory.AddMemberAsync(_org, SystemRoles.SiteManager, allSites: false));

        (await ListAsync(scoped)).Items.Select(i => i.Number).ShouldBe([onA.Number]);
        (await ListAsync(nothing)).TotalCount.ShouldBe(0);
        (await scoped.GetAsync(Url(onA.Id), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await Expect(scoped.GetAsync(Url(onB.Id), Ct), HttpStatusCode.NotFound, "issue.not_found");
        await Expect(scoped.PostEmptyAsync(Url(onB.Id, "start")), HttpStatusCode.NotFound, "issue.not_found");
        await Expect(scoped.PostJsonAsync(Url(onB.Id, "comments"), new CommentOnIssueCommand("hi", null)), HttpStatusCode.NotFound, "issue.not_found");
        await Expect(scoped.PutJsonAsync(Url(onB.Id, "priority"), new ChangeIssuePriorityCommand(IssuePriority.Low)), HttpStatusCode.NotFound, "issue.not_found");
        await Expect(scoped.PostJsonAsync(Url(onB.Id, "close"), new CloseIssueCommand(null)), HttpStatusCode.NotFound, "issue.not_found");

        var summary = await (await scoped.GetAsync("/api/v1/issues/summary", Ct)).ReadAsync<IssueSummaryResponse>();
        summary.ByStatus[IssueStatus.Open].ShouldBe(1);
    }

    // ---- Summary ----

    [Fact]
    public async Task The_summary_counts_issues_by_status_priority_and_overdue()
    {
        var (assignee, assigneeClient) = await MemberAsync(SystemRoles.SiteManager);
        var a1 = await RaiseAsync(_ownerClient, _siteA.Id, priority: IssuePriority.High, assigneeId: assignee.MembershipId);
        var a2 = await RaiseAsync(_ownerClient, _siteA.Id, priority: IssuePriority.High);
        var a3 = await RaiseAsync(_ownerClient, _siteA.Id, priority: IssuePriority.Low, assigneeId: assignee.MembershipId);
        var b1 = await RaiseAsync(_ownerClient, _siteB.Id, priority: IssuePriority.Critical);
        var b2 = await RaiseAsync(_ownerClient, _siteB.Id);
        (await _ownerClient.PostEmptyAsync(Url(a2.Id, "start"))).EnsureSuccessStatusCode();
        (await _ownerClient.PostJsonAsync(Url(a3.Id, "resolve"), new ResolveIssueCommand("Fixed", null))).EnsureSuccessStatusCode();
        (await _ownerClient.PostEmptyAsync(Url(b2.Id, "close"))).EnsureSuccessStatusCode();
        await SetDueAsync(b1.Id, DateTime.UtcNow.AddHours(-1));
        await SetDueAsync(a3.Id, DateTime.UtcNow.AddHours(-1)); // resolved, so not overdue

        var all = await (await _ownerClient.GetAsync("/api/v1/issues/summary", Ct)).ReadAsync<IssueSummaryResponse>();
        all.ByStatus[IssueStatus.Open].ShouldBe(2);
        all.ByStatus[IssueStatus.InProgress].ShouldBe(1);
        all.ByStatus[IssueStatus.Resolved].ShouldBe(1);
        all.ByStatus[IssueStatus.Closed].ShouldBe(1);
        all.Overdue.ShouldBe(1);
        all.ActiveByPriority[IssuePriority.Critical].ShouldBe(1);
        all.ActiveByPriority[IssuePriority.High].ShouldBe(2);
        all.ActiveByPriority[IssuePriority.Medium].ShouldBe(0);
        all.ActiveByPriority[IssuePriority.Low].ShouldBe(0);
        all.AssignedToMe.ShouldBe(0);

        var siteA = await (await _ownerClient.GetAsync($"/api/v1/issues/summary?siteId={_siteA.Id}", Ct)).ReadAsync<IssueSummaryResponse>();
        siteA.ByStatus.Values.Sum().ShouldBe(3);
        siteA.Overdue.ShouldBe(0);

        var mine = await (await assigneeClient.GetAsync("/api/v1/issues/summary", Ct)).ReadAsync<IssueSummaryResponse>();
        mine.AssignedToMe.ShouldBe(1); // a1 only; a3 is resolved
        a1.Number.ShouldBe(1);
    }

    // ---- Permissions ----

    [Fact]
    public async Task Team_members_can_raise_and_comment_but_not_manage()
    {
        var (_, team) = await MemberAsync(SystemRoles.TeamMember);
        var (manager, _) = await MemberAsync(SystemRoles.SiteManager);

        var issue = await RaiseAsync(team, _siteA.Id);
        (await team.PostJsonAsync(Url(issue.Id, "comments"), new CommentOnIssueCommand("Seen again", null))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await team.GetAsync(Url(issue.Id), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await team.PutJsonAsync(Url(issue.Id, "assignee"), new AssignIssueCommand(manager.MembershipId))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await team.PutJsonAsync(Url(issue.Id, "priority"), new ChangeIssuePriorityCommand(IssuePriority.Low))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await team.PutJsonAsync(Url(issue.Id, "due"), new SetIssueDueCommand(null))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await team.PostJsonAsync(Url(issue.Id, "close"), new CloseIssueCommand(null))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await team.PostJsonAsync(Url(issue.Id, "reopen"), new ReopenIssueCommand("x"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Members_without_issue_permissions_are_refused()
    {
        await _factory.WithDbAsync(_org, async db =>
        {
            db.Roles.Add(new Role { Name = "Viewer", Permissions = [Permissions.Issues.View] });
            db.Roles.Add(new Role { Name = "Nothing", Permissions = [Permissions.Sites.View] });
            return await db.SaveChangesAsync();
        });
        var viewer = _factory.CreateClient(await _factory.AddMemberAsync(_org, "Viewer"));
        var nothing = _factory.CreateClient(await _factory.AddMemberAsync(_org, "Nothing"));
        var issue = await RaiseAsync(_ownerClient, _siteA.Id);

        (await viewer.GetAsync(Url(issue.Id), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await viewer.PostEmptyAsync(Url(issue.Id, "start"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await viewer.PostJsonAsync(Url(issue.Id, "comments"), new CommentOnIssueCommand("x", null))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await RaiseRawAsync(viewer, _siteA.Id)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await nothing.GetAsync("/api/v1/issues", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await nothing.GetAsync("/api/v1/issues/summary", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _factory.CreateClient().GetAsync("/api/v1/issues", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Tenancy and concurrency ----

    [Fact]
    public async Task Another_organization_cannot_read_or_change_the_issue()
    {
        var issue = await RaiseAsync(_ownerClient, _siteA.Id);
        var other = await _factory.CreateOrganizationAsync("Other");
        var otherClient = _factory.CreateClient(other);
        var otherSite = await otherClient.CreateSiteAsync("Other site");
        var otherIssue = await RaiseAsync(otherClient, otherSite.Id);

        otherIssue.Number.ShouldBe(1);
        (await ListAsync(otherClient)).Items.ShouldHaveSingleItem().Id.ShouldBe(otherIssue.Id);
        (await ListAsync(_ownerClient)).Items.ShouldHaveSingleItem().Id.ShouldBe(issue.Id);
        await Expect(otherClient.GetAsync(Url(issue.Id), Ct), HttpStatusCode.NotFound, "issue.not_found");
        await Expect(otherClient.PostEmptyAsync(Url(issue.Id, "start")), HttpStatusCode.NotFound, "issue.not_found");
        await Expect(otherClient.PostJsonAsync(Url(issue.Id, "comments"), new CommentOnIssueCommand("x", null)), HttpStatusCode.NotFound, "issue.not_found");
        await Expect(otherClient.PostJsonAsync(Url(issue.Id, "close"), new CloseIssueCommand(null)), HttpStatusCode.NotFound, "issue.not_found");
        await Expect(otherClient.PutJsonAsync(Url(issue.Id, "assignee"), new AssignIssueCommand(other.MembershipId)), HttpStatusCode.NotFound, "issue.not_found");
        await Expect(RaiseRawAsync(otherClient, _siteA.Id), HttpStatusCode.BadRequest, "issue.unknown_site");

        // An assignee from another organization is not a valid assignee.
        await Expect(RaiseRawAsync(_ownerClient, _siteA.Id, assigneeId: other.MembershipId), HttpStatusCode.BadRequest, "issue.invalid_assignee");
        var summary = await (await otherClient.GetAsync("/api/v1/issues/summary", Ct)).ReadAsync<IssueSummaryResponse>();
        summary.ByStatus.Values.Sum().ShouldBe(1);
    }

    [Fact]
    public async Task Raising_issues_in_parallel_gives_unique_consecutive_numbers()
    {
        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(i => RaiseAsync(_ownerClient, _siteA.Id, $"Parallel {i}")));

        responses.Select(r => r.Number).Order().ShouldBe(Enumerable.Range(1, 10));
        (await ListAsync(_ownerClient)).TotalCount.ShouldBe(10);
    }
}
