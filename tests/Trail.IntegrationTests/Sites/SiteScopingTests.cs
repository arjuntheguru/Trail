using System.Net;
using Trail.Application.Common.Paging;
using Trail.Application.Common.Security;
using Trail.Application.Features.Sites;
using Trail.IntegrationTests.Infrastructure;

namespace Trail.IntegrationTests.Sites;

/// <summary>Members who are not assigned to all sites must only ever reach their own sites.</summary>
public sealed class SiteScopingTests(MySqlFixture mysql) : IAsyncLifetime
{
    private TrailApiFactory _factory = null!;
    private TestMember _owner = null!;
    private HttpClient _ownerClient = null!;
    private Guid _northRegion;
    private Guid _southRegion;
    private SiteResponse _northA = null!;
    private SiteResponse _northB = null!;
    private SiteResponse _southA = null!;
    private SiteResponse _unassigned = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = await TrailApiFactory.CreateAsync(mysql);
        _owner = await _factory.CreateOrganizationAsync();
        _ownerClient = _factory.CreateClient(_owner);

        _northRegion = (await _ownerClient.CreateRegionAsync("North")).Id;
        _southRegion = (await _ownerClient.CreateRegionAsync("South")).Id;
        _northA = await _ownerClient.CreateSiteAsync("North A", _northRegion);
        _northB = await _ownerClient.CreateSiteAsync("North B", _northRegion);
        _southA = await _ownerClient.CreateSiteAsync("South A", _southRegion);
        _unassigned = await _ownerClient.CreateSiteAsync("No Region");
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<(TestMember Member, HttpClient Client)> ScopedAsync(
        string role, Guid[]? siteIds = null, Guid[]? regionIds = null)
    {
        var member = await _factory.AddMemberAsync(_owner.OrganizationId, role, allSites: false);
        await _ownerClient.SetAccessAsync(member.MembershipId, false, siteIds, regionIds);
        return (member, _factory.CreateClient(member));
    }

    private static async Task<List<string>> VisibleSitesAsync(HttpClient client) =>
        (await (await client.GetAsync("/api/v1/sites?pageSize=100", Ct)).ReadAsync<PagedResult<SiteResponse>>())
            .Items.Select(s => s.Name).ToList();

    [Fact]
    public async Task A_member_with_all_sites_sees_everything()
    {
        var member = _factory.CreateClient(await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.TeamMember, allSites: true));

        (await VisibleSitesAsync(member)).ShouldBe(["No Region", "North A", "North B", "South A"]);
    }

    [Fact]
    public async Task A_member_with_no_assignments_sees_nothing()
    {
        var (_, client) = await ScopedAsync(SystemRoles.TeamMember);

        (await VisibleSitesAsync(client)).ShouldBeEmpty();
        (await client.GetAsync($"/api/v1/sites/{_northA.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_member_sees_directly_assigned_sites_and_the_sites_of_assigned_regions()
    {
        var (_, client) = await ScopedAsync(SystemRoles.SiteManager, siteIds: [_southA.Id], regionIds: [_northRegion]);

        (await VisibleSitesAsync(client)).ShouldBe(["North A", "North B", "South A"]);
        (await client.GetAsync($"/api/v1/sites/{_northB.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync($"/api/v1/sites/{_southA.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var hidden = await client.GetAsync($"/api/v1/sites/{_unassigned.Id}", Ct);
        hidden.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await hidden.ProblemCodeAsync()).ShouldBe("site.not_found");
    }

    [Fact]
    public async Task Filters_cannot_widen_a_members_scope()
    {
        var (_, client) = await ScopedAsync(SystemRoles.TeamMember, siteIds: [_northA.Id]);

        var byRegion = await client.GetAsync($"/api/v1/sites?regionId={_southRegion}", Ct);
        (await byRegion.ReadAsync<PagedResult<SiteResponse>>()).Items.ShouldBeEmpty();
        var search = await client.GetAsync("/api/v1/sites?search=South", Ct);
        (await search.ReadAsync<PagedResult<SiteResponse>>()).TotalCount.ShouldBe(0);
        (await VisibleSitesAsync(client)).ShouldBe(["North A"]);
    }

    [Fact]
    public async Task A_new_site_in_an_assigned_region_becomes_visible_and_a_deleted_one_disappears()
    {
        var (_, client) = await ScopedAsync(SystemRoles.SiteManager, regionIds: [_northRegion]);

        var added = await _ownerClient.CreateSiteAsync("North C", _northRegion);
        (await VisibleSitesAsync(client)).ShouldBe(["North A", "North B", "North C"]);

        await _ownerClient.DeleteAsync($"/api/v1/sites/{added.Id}", Ct);
        (await VisibleSitesAsync(client)).ShouldBe(["North A", "North B"]);
    }

    [Fact]
    public async Task A_scoped_manager_can_only_change_sites_in_scope()
    {
        var (_, client) = await ScopedAsync(SystemRoles.AreaManager, regionIds: [_northRegion]);
        var update = new UpdateSiteCommand("North A renamed", null, _northRegion, null, null, null, null, null);

        (await client.PutJsonAsync($"/api/v1/sites/{_northA.Id}", update)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostEmptyAsync($"/api/v1/sites/{_northA.Id}/deactivate")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await client.PutJsonAsync($"/api/v1/sites/{_southA.Id}", update with { Name = "Hijack", RegionId = _southRegion }))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PostEmptyAsync($"/api/v1/sites/{_southA.Id}/deactivate")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PostEmptyAsync($"/api/v1/sites/{_unassigned.Id}/activate")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.DeleteAsync($"/api/v1/sites/{_southA.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var untouched = await (await _ownerClient.GetAsync($"/api/v1/sites/{_southA.Id}", Ct)).ReadAsync<SiteResponse>();
        (untouched.Name, untouched.IsActive).ShouldBe(("South A", true));
    }

    [Fact]
    public async Task A_scoped_manager_cannot_move_a_site_into_a_foreign_region()
    {
        var (_, client) = await ScopedAsync(SystemRoles.AreaManager, regionIds: [_northRegion]);
        var move = new UpdateSiteCommand("North A", null, _southRegion, null, null, null, null, null);

        var response = await client.PutJsonAsync($"/api/v1/sites/{_northA.Id}", move);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.ProblemCodeAsync()).ShouldBe("site.region_out_of_scope");
    }

    [Fact]
    public async Task A_scoped_manager_can_only_create_sites_in_their_regions()
    {
        var (_, client) = await ScopedAsync(SystemRoles.AreaManager, regionIds: [_northRegion]);

        var allowed = await client.PostJsonAsync("/api/v1/sites", Create("North D", _northRegion));
        allowed.StatusCode.ShouldBe(HttpStatusCode.Created);
        // ... and they can see what they created.
        (await VisibleSitesAsync(client)).ShouldContain("North D");

        foreach (var region in new Guid?[] { _southRegion, null, Guid.NewGuid() })
        {
            var denied = await client.PostJsonAsync("/api/v1/sites", Create($"Denied {region}", region));
            denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            (await denied.ProblemCodeAsync()).ShouldBe("site.region_out_of_scope");
        }

        (await _ownerClient.PostJsonAsync("/api/v1/sites", Create("Owner site", null))).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_suspended_member_sees_nothing_even_with_a_valid_token()
    {
        var member = await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.TeamMember, allSites: true);
        var client = _factory.CreateClient(member);
        (await VisibleSitesAsync(client)).Count.ShouldBe(4);

        await _factory.WithDbAsync(_owner.OrganizationId, async db =>
        {
            var membership = db.Memberships.Single(m => m.Id == member.MembershipId);
            membership.Status = Domain.Identity.MembershipStatus.Suspended;
            return await db.SaveChangesAsync(Ct);
        });

        (await VisibleSitesAsync(client)).ShouldBeEmpty();
    }

    private static CreateSiteCommand Create(string name, Guid? regionId) =>
        new(name, null, regionId, null, null, null, null, null);
}
