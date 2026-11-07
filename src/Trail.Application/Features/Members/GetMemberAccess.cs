using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;

namespace Trail.Application.Features.Members;

public sealed class GetMemberAccessHandler(IAppDbContext db)
{
    public async Task<Result<MemberAccessResponse>> Handle(Guid membershipId, CancellationToken ct)
    {
        var allSites = await db.Memberships.AsNoTracking()
            .Where(m => m.Id == membershipId)
            .Select(m => (bool?)m.AllSites)
            .FirstOrDefaultAsync(ct);
        if (allSites is null) return Error.NotFound("member.not_found", "Member not found.");

        return new MemberAccessResponse(
            allSites.Value,
            await LiveSiteIdsAsync(db, membershipId, ct),
            await LiveRegionIdsAsync(db, membershipId, ct),
            await LiveDepartmentIdsAsync(db, membershipId, ct));
    }

    // Joining to the target tables applies their soft-delete filters, so deleted sites, regions and
    // departments are not reported even if an assignment row remains.
    internal static Task<List<Guid>> LiveSiteIdsAsync(IAppDbContext db, Guid membershipId, CancellationToken ct) =>
        db.Sites.AsNoTracking()
            .Where(s => db.MembershipSites.Any(x => x.MembershipId == membershipId && x.SiteId == s.Id))
            .OrderBy(s => s.Id).Select(s => s.Id).ToListAsync(ct);

    internal static Task<List<Guid>> LiveRegionIdsAsync(IAppDbContext db, Guid membershipId, CancellationToken ct) =>
        db.Regions.AsNoTracking()
            .Where(r => db.MembershipRegions.Any(x => x.MembershipId == membershipId && x.RegionId == r.Id))
            .OrderBy(r => r.Id).Select(r => r.Id).ToListAsync(ct);

    internal static Task<List<Guid>> LiveDepartmentIdsAsync(IAppDbContext db, Guid membershipId, CancellationToken ct) =>
        db.Departments.AsNoTracking()
            .Where(d => db.MembershipDepartments.Any(x => x.MembershipId == membershipId && x.DepartmentId == d.Id))
            .OrderBy(d => d.Id).Select(d => d.Id).ToListAsync(ct);
}
