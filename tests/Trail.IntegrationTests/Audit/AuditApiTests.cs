using System.Net;
using Trail.Application.Common.Paging;
using Trail.Application.Common.Security;
using Trail.Application.Features.Audit;
using Trail.Application.Features.Sites;
using Trail.Domain.Auditing;
using Trail.IntegrationTests.Infrastructure;

namespace Trail.IntegrationTests.Audit;

public sealed class AuditApiTests(MySqlFixture mysql) : IAsyncLifetime
{
    private TrailApiFactory _factory = null!;
    private TestMember _owner = null!;
    private HttpClient _ownerClient = null!;
    private SiteResponse _leeds = null!;
    private SiteResponse _york = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = await TrailApiFactory.CreateAsync(mysql);
        _owner = await _factory.CreateOrganizationAsync();
        _ownerClient = _factory.CreateClient(_owner);
        _leeds = await _ownerClient.CreateSiteAsync("Leeds");
        _york = await _ownerClient.CreateSiteAsync("York");
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<PagedResult<AuditEntryResponse>> TrailAsync(HttpClient client, string query = "") =>
        await (await client.GetAsync($"/api/v1/audit{query}", Ct)).ReadAsync<PagedResult<AuditEntryResponse>>();

    [Fact]
    public async Task Changes_made_through_the_api_appear_newest_first_with_the_actor()
    {
        (await _ownerClient.PostEmptyAsync($"/api/v1/sites/{_leeds.Id}/deactivate")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var trail = await TrailAsync(_ownerClient, $"?entityId={_leeds.Id}");

        trail.Items.Select(a => a.Action).ShouldBe([AuditAction.Updated, AuditAction.Created]);
        var update = trail.Items[0];
        update.EntityType.ShouldBe("Site");
        update.ActorId.ShouldBe(_owner.UserId);
        update.ActorName.ShouldBe("Test User");
        update.SiteId.ShouldBe(_leeds.Id);
        update.TraceId.ShouldNotBeNullOrEmpty();
        var change = update.Changes.ShouldHaveSingleItem();
        change.Key.ShouldBe("IsActive");
        change.Value.Old!.Value.GetBoolean().ShouldBeTrue();
        change.Value.New!.Value.GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Filters_narrow_the_trail()
    {
        await _ownerClient.CreateRegionAsync("North");

        (await TrailAsync(_ownerClient, "?entityType=Region")).Items.ShouldHaveSingleItem().Action.ShouldBe(AuditAction.Created);
        (await TrailAsync(_ownerClient, $"?siteId={_york.Id}")).Items.ShouldAllBe(a => a.SiteId == _york.Id);
        (await TrailAsync(_ownerClient, "?action=Deleted")).TotalCount.ShouldBe(0);
        (await TrailAsync(_ownerClient, $"?actorId={Guid.NewGuid()}")).TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Members_limited_to_some_sites_see_only_those_sites_rows()
    {
        var area = await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.AreaManager, allSites: false);
        await _ownerClient.SetAccessAsync(area.MembershipId, false, siteIds: [_leeds.Id]);
        await _ownerClient.CreateRegionAsync("North");

        var trail = await TrailAsync(_factory.CreateClient(area));

        trail.Items.ShouldNotBeEmpty();
        trail.Items.ShouldAllBe(a => a.SiteId == _leeds.Id);
    }

    [Fact]
    public async Task The_trail_needs_the_audit_permission_and_stays_in_the_organization()
    {
        var teamMember = _factory.CreateClient(await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.TeamMember));
        (await teamMember.GetAsync("/api/v1/audit", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var other = _factory.CreateClient(await _factory.CreateOrganizationAsync("Other"));
        (await TrailAsync(other, $"?entityId={_leeds.Id}")).TotalCount.ShouldBe(0);
    }
}
