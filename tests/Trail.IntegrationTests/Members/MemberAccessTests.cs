using System.Net;
using Trail.Application.Common.Paging;
using Trail.Application.Common.Security;
using Trail.Application.Features.Members;
using Trail.Application.Features.Sites;
using Trail.IntegrationTests.Infrastructure;

namespace Trail.IntegrationTests.Members;

public sealed class MemberAccessTests(MySqlFixture mysql) : IAsyncLifetime
{
    private TrailApiFactory _factory = null!;
    private TestMember _owner = null!;
    private HttpClient _ownerClient = null!;
    private Guid _northRegion;
    private Guid _southRegion;
    private SiteResponse _northSite = null!;
    private SiteResponse _southSite = null!;
    private Guid _kitchen;

    public async ValueTask InitializeAsync()
    {
        _factory = await TrailApiFactory.CreateAsync(mysql);
        _owner = await _factory.CreateOrganizationAsync();
        _ownerClient = _factory.CreateClient(_owner);
        _northRegion = (await _ownerClient.CreateRegionAsync("North")).Id;
        _southRegion = (await _ownerClient.CreateRegionAsync("South")).Id;
        _northSite = await _ownerClient.CreateSiteAsync("North A", _northRegion);
        _southSite = await _ownerClient.CreateSiteAsync("South A", _southRegion);
        _kitchen = (await _ownerClient.CreateDepartmentAsync("Kitchen")).Id;
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Describe(MemberAccessResponse r) =>
        $"{r.AllSites}|{string.Join(',', r.SiteIds.Order())}|{string.Join(',', r.RegionIds.Order())}|{string.Join(',', r.DepartmentIds.Order())}";

    /// <summary>Compares access ignoring the concrete collection types and the order of ids.</summary>
    private static void ShouldMatch(MemberAccessResponse actual, MemberAccessResponse expected) =>
        Describe(actual).ShouldBe(Describe(expected));

    private static string AccessUrl(TestMember member) => $"/api/v1/members/{member.MembershipId}/access";

    private async Task<MemberAccessResponse> GetAccessAsync(HttpClient client, TestMember member) =>
        await (await client.GetAsync(AccessUrl(member), Ct)).ReadAsync<MemberAccessResponse>();

    [Fact]
    public async Task Access_can_be_read_and_replaced()
    {
        var member = await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.SiteManager, allSites: true);
        ShouldMatch(await GetAccessAsync(_ownerClient, member), new MemberAccessResponse(true, [], [], []));

        var put = await _ownerClient.PutJsonAsync(AccessUrl(member),
            new SetMemberAccessCommand(false, [_northSite.Id, _southSite.Id], [_northRegion], [_kitchen]));
        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        var expected = new MemberAccessResponse(false, [.. new[] { _northSite.Id, _southSite.Id }.Order()], [_northRegion], [_kitchen]);
        ShouldMatch(await put.ReadAsync<MemberAccessResponse>(), expected);
        ShouldMatch(await GetAccessAsync(_ownerClient, member), expected);

        // Replace semantics: omitted and removed entries go away; duplicates are tolerated.
        await _ownerClient.SetAccessAsync(member.MembershipId, false, siteIds: [_southSite.Id, _southSite.Id]);
        ShouldMatch(await GetAccessAsync(_ownerClient, member), new MemberAccessResponse(false, [_southSite.Id], [], []));

        await _ownerClient.SetAccessAsync(member.MembershipId, true);
        ShouldMatch(await GetAccessAsync(_ownerClient, member), new MemberAccessResponse(true, [], [], []));
    }

    [Fact]
    public async Task Changing_access_takes_effect_for_the_member()
    {
        var member = await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.TeamMember, allSites: true);
        var client = _factory.CreateClient(member);

        await _ownerClient.SetAccessAsync(member.MembershipId, false, siteIds: [_northSite.Id]);

        var visible = await (await client.GetAsync("/api/v1/sites", Ct)).ReadAsync<PagedResult<SiteResponse>>();
        visible.Items.ShouldHaveSingleItem().Id.ShouldBe(_northSite.Id);
    }

    [Fact]
    public async Task Deleted_sites_regions_and_departments_are_not_reported()
    {
        var member = await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.TeamMember, allSites: false);
        var extraRegion = (await _ownerClient.CreateRegionAsync("Temp")).Id;
        var extraDepartment = (await _ownerClient.CreateDepartmentAsync("Temp")).Id;
        await _ownerClient.SetAccessAsync(member.MembershipId, false, [_northSite.Id], [extraRegion], [extraDepartment]);

        await _ownerClient.DeleteAsync($"/api/v1/sites/{_northSite.Id}", Ct);
        await _ownerClient.DeleteAsync($"/api/v1/regions/{extraRegion}", Ct);
        await _ownerClient.DeleteAsync($"/api/v1/departments/{extraDepartment}", Ct);

        ShouldMatch(await GetAccessAsync(_ownerClient, member), new MemberAccessResponse(false, [], [], []));
    }

    [Fact]
    public async Task Unknown_ids_are_rejected_and_leave_access_unchanged()
    {
        var member = await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.TeamMember, allSites: true);
        var otherOrg = _factory.CreateClient(await _factory.CreateOrganizationAsync("Other Org"));
        var foreignSite = await otherOrg.CreateSiteAsync("Foreign");

        foreach (var command in new[]
                 {
                     new SetMemberAccessCommand(false, [Guid.NewGuid()], null, null),
                     new SetMemberAccessCommand(false, null, [Guid.NewGuid()], null),
                     new SetMemberAccessCommand(false, null, null, [Guid.NewGuid()]),
                     new SetMemberAccessCommand(false, [foreignSite.Id], null, null),
                     new SetMemberAccessCommand(false, [_northSite.Id, Guid.NewGuid()], null, null),
                 })
        {
            var response = await _ownerClient.PutJsonAsync(AccessUrl(member), command);
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await response.ProblemCodeAsync()).ShouldBe("member.unknown_ids");
        }

        (await _ownerClient.PutJsonAsync(AccessUrl(member), new SetMemberAccessCommand(false, [Guid.Empty], null, null)))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await GetAccessAsync(_ownerClient, member)).AllSites.ShouldBeTrue();
    }

    [Fact]
    public async Task An_unknown_member_returns_not_found()
    {
        var url = $"/api/v1/members/{Guid.NewGuid()}/access";

        var get = await _ownerClient.GetAsync(url, Ct);
        get.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await get.ProblemCodeAsync()).ShouldBe("member.not_found");
        (await _ownerClient.PutJsonAsync(url, new SetMemberAccessCommand(true, null, null, null)))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Nobody_can_change_their_own_access()
    {
        var response = await _ownerClient.PutJsonAsync(AccessUrl(_owner), new SetMemberAccessCommand(false, null, null, null));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.ProblemCodeAsync()).ShouldBe("member.cannot_change_own_access");
        (await GetAccessAsync(_ownerClient, _owner)).AllSites.ShouldBeTrue();
    }

    [Fact]
    public async Task A_scoped_admin_cannot_grant_more_than_they_have()
    {
        var admin = await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.Admin, allSites: false);
        await _ownerClient.SetAccessAsync(admin.MembershipId, false, regionIds: [_northRegion]);
        var adminClient = _factory.CreateClient(admin);
        var target = await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.TeamMember, allSites: false);

        async Task ShouldBeForbidden(SetMemberAccessCommand command)
        {
            var response = await adminClient.PutJsonAsync(AccessUrl(target), command);
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            (await response.ProblemCodeAsync()).ShouldBe("member.access_out_of_scope");
        }

        await ShouldBeForbidden(new SetMemberAccessCommand(true, null, null, null));
        await ShouldBeForbidden(new SetMemberAccessCommand(false, [_southSite.Id], null, null));
        await ShouldBeForbidden(new SetMemberAccessCommand(false, null, [_southRegion], null));
        await ShouldBeForbidden(new SetMemberAccessCommand(false, [_northSite.Id, _southSite.Id], null, null));
        ShouldMatch(await GetAccessAsync(_ownerClient, target), new MemberAccessResponse(false, [], [], []));

        // Within their own scope (a site of their region, the region itself, any department) it works.
        (await adminClient.PutJsonAsync(AccessUrl(target),
                new SetMemberAccessCommand(false, [_northSite.Id], [_northRegion], [_kitchen])))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_scoped_admin_cannot_edit_a_member_with_wider_access()
    {
        var admin = await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.Admin, allSites: false);
        await _ownerClient.SetAccessAsync(admin.MembershipId, false, regionIds: [_northRegion]);
        var adminClient = _factory.CreateClient(admin);

        var everywhere = await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.TeamMember, allSites: true);
        var southern = await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.TeamMember, allSites: false);
        await _ownerClient.SetAccessAsync(southern.MembershipId, false, siteIds: [_southSite.Id]);

        foreach (var target in new[] { everywhere, southern })
        {
            var response = await adminClient.PutJsonAsync(AccessUrl(target),
                new SetMemberAccessCommand(false, [_northSite.Id], null, null));
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            (await response.ProblemCodeAsync()).ShouldBe("member.access_out_of_scope");
        }

        (await GetAccessAsync(_ownerClient, southern)).SiteIds.ShouldBe([_southSite.Id]);
        (await GetAccessAsync(_ownerClient, everywhere)).AllSites.ShouldBeTrue();
    }

    [Fact]
    public async Task Permissions_are_enforced()
    {
        var target = await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.TeamMember);
        var siteManager = _factory.CreateClient(await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.SiteManager));
        var teamMember = _factory.CreateClient(await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.TeamMember));

        // Site Managers can view members but not manage them; Team Members can do neither.
        (await siteManager.GetAsync(AccessUrl(target), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await siteManager.PutJsonAsync(AccessUrl(target), new SetMemberAccessCommand(true, null, null, null)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await teamMember.GetAsync(AccessUrl(target), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _factory.CreateClient().GetAsync(AccessUrl(target), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Another_organization_cannot_read_or_change_a_members_access()
    {
        var member = await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.TeamMember, allSites: false);
        await _ownerClient.SetAccessAsync(member.MembershipId, false, siteIds: [_northSite.Id]);
        var otherOwner = _factory.CreateClient(await _factory.CreateOrganizationAsync("Other Org"));

        (await otherOwner.GetAsync(AccessUrl(member), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await otherOwner.PutJsonAsync(AccessUrl(member), new SetMemberAccessCommand(true, null, null, null)))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ShouldMatch(await GetAccessAsync(_ownerClient, member), new MemberAccessResponse(false, [_northSite.Id], [], []));
    }
}
