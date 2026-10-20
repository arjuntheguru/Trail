using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Domain.Identity;
using Trail.Domain.Organizations;

namespace Trail.Application.Common.Security;

/// <summary>
/// Resolves the caller's <see cref="SiteScope"/>. Platform admins see everything; callers without an active
/// membership in the current organization see nothing; everyone else sees their membership's assigned sites
/// plus the live sites of its assigned regions, or everything when the membership has AllSites.
/// </summary>
public sealed class SiteAccess(ICurrentUser user, IAppDbContext db) : ISiteAccess
{
    private Task<SiteScope>? _scope;

    public Task<SiteScope> GetScopeAsync(CancellationToken ct) => _scope ??= LoadAsync(ct);

    public async Task<bool> CanAccessSiteAsync(Guid siteId, CancellationToken ct) =>
        (await GetScopeAsync(ct)).Includes(siteId);

    public IQueryable<Site> ApplyTo(IQueryable<Site> sites, SiteScope scope)
    {
        if (scope.AllSites) return sites;
        if (scope.SiteIds.Count == 0) return sites.Where(_ => false);

        var ids = scope.SiteIds.ToList();
        return sites.Where(s => ids.Contains(s.Id));
    }

    private async Task<SiteScope> LoadAsync(CancellationToken ct)
    {
        if (user.IsPlatformAdmin) return SiteScope.Everything;
        if (user.MembershipId is not { } membershipId) return SiteScope.Nothing;

        // The tenant filter confines this to the caller's organization, so a membership id from another
        // organization resolves to nothing.
        var membership = await db.Memberships.AsNoTracking()
            .Where(m => m.Id == membershipId)
            .Select(m => new { m.Status, m.AllSites })
            .FirstOrDefaultAsync(ct);

        if (membership is null || membership.Status != MembershipStatus.Active) return SiteScope.Nothing;
        if (membership.AllSites) return SiteScope.Everything;

        // Joining to Regions/Sites applies their soft-delete filters, so deleted ones grant nothing.
        var regionIds = await db.Regions.AsNoTracking()
            .Where(r => db.MembershipRegions.Any(mr => mr.MembershipId == membershipId && mr.RegionId == r.Id))
            .Select(r => r.Id)
            .ToListAsync(ct);

        var directSiteIds = await db.Sites.AsNoTracking()
            .Where(s => db.MembershipSites.Any(ms => ms.MembershipId == membershipId && ms.SiteId == s.Id))
            .Select(s => s.Id)
            .ToListAsync(ct);

        var regionSiteIds = regionIds.Count == 0
            ? []
            : await db.Sites.AsNoTracking()
                .Where(s => s.RegionId != null && regionIds.Contains(s.RegionId.Value))
                .Select(s => s.Id)
                .ToListAsync(ct);

        return new SiteScope(false, directSiteIds.Concat(regionSiteIds).ToHashSet(), regionIds.ToHashSet());
    }
}
