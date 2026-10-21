using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Domain.Identity;

namespace Trail.Application.Features.Notifications;

/// <summary>Finds the memberships that notifications should reach. Everything is confined to the current tenant.</summary>
public sealed class MembershipDirectory(IAppDbContext db)
{
    /// <summary>
    /// Active memberships that reach <paramref name="siteId"/> (all-sites, a direct assignment, or an assignment to its
    /// region) and whose role holds <paramref name="permission"/>. Same reach rule as assignee validation for issues.
    /// </summary>
    public async Task<IReadOnlyList<SiteMember>> HoldingAtSiteAsync(Guid siteId, string permission, CancellationToken ct)
    {
        // Sites may be soft-deleted while old issues still point at them; their region still counts.
        var regionId = await db.Sites.IgnoreQueryFilters([QueryFilters.SoftDelete])
            .Where(s => s.Id == siteId).Select(s => s.RegionId).FirstOrDefaultAsync(ct);

        var rows = await db.Memberships.AsNoTracking()
            .Where(m => m.Status == MembershipStatus.Active
                        && (m.AllSites
                            || db.MembershipSites.Any(ms => ms.MembershipId == m.Id && ms.SiteId == siteId)
                            || (regionId != null && db.MembershipRegions.Any(mr => mr.MembershipId == m.Id && mr.RegionId == regionId))))
            .Select(m => new
            {
                m.Id,
                m.UserId,
                m.AllSites,
                Permissions = m.Role.Permissions,
                ViaRegion = regionId != null && db.MembershipRegions.Any(mr => mr.MembershipId == m.Id && mr.RegionId == regionId),
            })
            .ToListAsync(ct);

        // Permissions live in a json column, so they are checked here rather than in SQL.
        return rows.Where(r => r.Permissions.Contains(permission))
            .Select(r => new SiteMember(r.Id, r.UserId, EscalationPolicy.LevelOf(r.AllSites, r.ViaRegion)))
            .ToList();
    }

    /// <summary>The user's membership in the current organization, or null (system actor, other organization).</summary>
    public async Task<Guid?> MembershipOfUserAsync(Guid? userId, CancellationToken ct)
    {
        if (userId is not { } id) return null;
        return await db.Memberships.AsNoTracking().Where(m => m.UserId == id).Select(m => (Guid?)m.Id).FirstOrDefaultAsync(ct);
    }
}
