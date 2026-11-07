using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Security;
using Trail.Domain.Identity;
using Trail.Domain.Organizations;
using Trail.IntegrationTests.Infrastructure;

namespace Trail.IntegrationTests.Sites;

/// <summary>The rules that decide which sites a caller can reach, tested directly against a real database.</summary>
public sealed class SiteAccessTests(MySqlFixture mysql) : IAsyncLifetime
{
    private TrailApiFactory _factory = null!;
    private TestMember _owner = null!;
    private Guid _org;
    private Region _north = null!;
    private Region _south = null!;
    private Site _northA = null!;
    private Site _northInactive = null!;
    private Site _southA = null!;
    private Site _southB = null!;
    private Site _unassigned = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = await TrailApiFactory.CreateAsync(mysql);
        _owner = await _factory.CreateOrganizationAsync();
        _org = _owner.OrganizationId;

        _north = new Region { Name = "North" };
        _south = new Region { Name = "South" };
        _northA = NewSite("North A", _north);
        _northInactive = NewSite("North Inactive", _north);
        _northInactive.IsActive = false;
        _southA = NewSite("South A", _south);
        _southB = NewSite("South B", _south);
        _unassigned = NewSite("Unassigned", null);
        await _factory.WithDbAsync(_org, async db =>
        {
            db.Regions.AddRange(_north, _south);
            db.Sites.AddRange(_northA, _northInactive, _southA, _southB, _unassigned);
            return await db.SaveChangesAsync(Ct);
        });
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Site NewSite(string name, Region? region) =>
        new() { Name = name, TimeZone = "UTC", RegionId = region?.Id };

    private static ICurrentUser User(Guid? membershipId, bool platformAdmin = false)
    {
        var user = Substitute.For<ICurrentUser>();
        user.MembershipId.Returns(membershipId);
        user.IsPlatformAdmin.Returns(platformAdmin);
        return user;
    }

    private async Task<Guid> MemberAsync(
        bool allSites, Site[]? sites = null, Region[]? regions = null, MembershipStatus? status = null)
    {
        var member = await _factory.AddMemberAsync(_org, SystemRoles.TeamMember, allSites);
        await _factory.WithDbAsync(_org, async db =>
        {
            foreach (var site in sites ?? [])
                db.MembershipSites.Add(new MembershipSite { MembershipId = member.MembershipId, SiteId = site.Id });
            foreach (var region in regions ?? [])
                db.MembershipRegions.Add(new MembershipRegion { MembershipId = member.MembershipId, RegionId = region.Id });
            if (status is { } s)
                (await db.Memberships.SingleAsync(m => m.Id == member.MembershipId, Ct)).Status = s;
            return await db.SaveChangesAsync(Ct);
        });
        return member.MembershipId;
    }

    private Task<T> WithAccessAsync<T>(ICurrentUser user, Func<SiteAccess, IAppDbContext, Task<T>> action) =>
        _factory.WithDbAsync(_org, db => action(new SiteAccess(user, db), db));

    private async Task<SiteScope> ScopeOfAsync(ICurrentUser user) =>
        await WithAccessAsync(user, (access, _) => access.GetScopeAsync(Ct));

    private static HashSet<Guid> Ids(params Site[] sites) => sites.Select(s => s.Id).ToHashSet();

    [Fact]
    public async Task Platform_admins_reach_every_site()
    {
        var scope = await ScopeOfAsync(User(null, platformAdmin: true));

        scope.AllSites.ShouldBeTrue();
        scope.Includes(Guid.NewGuid()).ShouldBeTrue();
    }

    [Fact]
    public async Task Callers_without_a_membership_reach_nothing()
    {
        var scope = await ScopeOfAsync(User(null));

        scope.AllSites.ShouldBeFalse();
        scope.SiteIds.ShouldBeEmpty();
        scope.RegionIds.ShouldBeEmpty();
        scope.Includes(_northA.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task An_unknown_membership_reaches_nothing()
    {
        var scope = await ScopeOfAsync(User(Guid.NewGuid()));

        (scope.AllSites, scope.SiteIds.Count).ShouldBe((false, 0));
    }

    [Fact]
    public async Task A_membership_from_another_organization_reaches_nothing()
    {
        var other = await _factory.CreateOrganizationAsync("Other Org");

        var scope = await ScopeOfAsync(User(other.MembershipId));

        (scope.AllSites, scope.SiteIds.Count).ShouldBe((false, 0));
    }

    [Theory]
    [InlineData(MembershipStatus.Invited)]
    [InlineData(MembershipStatus.Suspended)]
    public async Task Memberships_that_are_not_active_reach_nothing_even_with_all_sites(MembershipStatus status)
    {
        var membership = await MemberAsync(allSites: true, status: status);

        var scope = await ScopeOfAsync(User(membership));

        (scope.AllSites, scope.SiteIds.Count).ShouldBe((false, 0));
    }

    [Fact]
    public async Task Memberships_with_all_sites_reach_everything()
    {
        var scope = await ScopeOfAsync(User(await MemberAsync(allSites: true)));

        scope.AllSites.ShouldBeTrue();
        scope.Includes(_unassigned.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task Scoped_memberships_reach_assigned_sites_plus_every_site_of_assigned_regions()
    {
        var membership = await MemberAsync(allSites: false, sites: [_southA, _unassigned], regions: [_north]);

        var scope = await ScopeOfAsync(User(membership));

        scope.AllSites.ShouldBeFalse();
        // Inactive sites remain reachable so that managers can reactivate them.
        scope.SiteIds.ShouldBe(Ids(_southA, _unassigned, _northA, _northInactive), ignoreOrder: true);
        scope.RegionIds.ShouldBe([_north.Id]);
        scope.Includes(_southB.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task Assigning_all_sites_off_with_nothing_assigned_reaches_nothing()
    {
        var scope = await ScopeOfAsync(User(await MemberAsync(allSites: false)));

        (scope.AllSites, scope.SiteIds.Count, scope.RegionIds.Count).ShouldBe((false, 0, 0));
    }

    [Fact]
    public async Task Deleted_sites_and_regions_grant_nothing()
    {
        var membership = await MemberAsync(allSites: false, sites: [_southA], regions: [_north]);
        await _factory.WithDbAsync(_org, async db =>
        {
            db.Sites.Remove(await db.Sites.SingleAsync(s => s.Id == _southA.Id, Ct));
            db.Sites.Remove(await db.Sites.SingleAsync(s => s.Id == _northInactive.Id, Ct));
            return await db.SaveChangesAsync(Ct);
        });

        var scope = await ScopeOfAsync(User(membership));
        scope.SiteIds.ShouldBe(Ids(_northA), ignoreOrder: true);

        await _factory.WithDbAsync(_org, async db =>
        {
            db.Sites.Remove(await db.Sites.SingleAsync(s => s.Id == _northA.Id, Ct));
            db.Regions.Remove(await db.Regions.SingleAsync(r => r.Id == _north.Id, Ct));
            return await db.SaveChangesAsync(Ct);
        });

        var afterRegionDeleted = await ScopeOfAsync(User(membership));
        (afterRegionDeleted.SiteIds.Count, afterRegionDeleted.RegionIds.Count).ShouldBe((0, 0));
    }

    [Fact]
    public async Task The_scope_is_resolved_once_per_instance()
    {
        var membership = await MemberAsync(allSites: false, sites: [_southA]);

        await _factory.WithDbAsync(_org, async db =>
        {
            var access = new SiteAccess(User(membership), db);
            var first = await access.GetScopeAsync(Ct);

            db.MembershipSites.Add(new MembershipSite { MembershipId = membership, SiteId = _southB.Id });
            await db.SaveChangesAsync(Ct);

            (await access.GetScopeAsync(Ct)).ShouldBeSameAs(first);
            (await access.CanAccessSiteAsync(_southA.Id, Ct)).ShouldBeTrue();
            (await access.CanAccessSiteAsync(_southB.Id, Ct)).ShouldBeFalse();
            (await new SiteAccess(User(membership), db).CanAccessSiteAsync(_southB.Id, Ct)).ShouldBeTrue();
            return 0;
        });
    }

    [Fact]
    public async Task ApplyTo_restricts_queries_to_the_scope()
    {
        var membership = await MemberAsync(allSites: false, sites: [_southA], regions: [_north]);

        var names = await WithAccessAsync(User(membership), async (access, db) =>
        {
            var scope = await access.GetScopeAsync(Ct);
            return await access.ApplyTo(db.Sites.AsNoTracking(), scope).OrderBy(s => s.Name).Select(s => s.Name).ToListAsync(Ct);
        });

        names.ShouldBe(["North A", "North Inactive", "South A"]);
    }

    [Fact]
    public async Task ApplyTo_returns_everything_for_all_sites_and_nothing_for_an_empty_scope()
    {
        var everything = await WithAccessAsync(User(null, platformAdmin: true), async (access, db) =>
            await access.ApplyTo(db.Sites.AsNoTracking(), await access.GetScopeAsync(Ct)).CountAsync(Ct));
        var nothing = await WithAccessAsync(User(null), async (access, db) =>
            await access.ApplyTo(db.Sites.AsNoTracking(), await access.GetScopeAsync(Ct)).CountAsync(Ct));

        (everything, nothing).ShouldBe((5, 0));
    }
}
