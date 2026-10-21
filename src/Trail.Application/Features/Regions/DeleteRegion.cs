using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;

namespace Trail.Application.Features.Regions;

public sealed class DeleteRegionHandler(IAppDbContext db)
{
    public async Task<Result> Handle(Guid id, CancellationToken ct)
    {
        var region = await db.Regions.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (region is null) return Error.NotFound("region.not_found", "Region not found.");

        if (await db.Sites.AnyAsync(s => s.RegionId == id, ct))
            return Error.Conflict("region.has_sites", "Move or delete the sites in this region first.");

        // Access grants to a deleted region would be dead rows; drop them.
        db.MembershipRegions.RemoveRange(await db.MembershipRegions.Where(x => x.RegionId == id).ToListAsync(ct));
        db.Regions.Remove(region);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
