using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;

namespace Trail.Application.Features.Regions;

public sealed class ListRegionsHandler(IAppDbContext db)
{
    public async Task<IReadOnlyList<RegionResponse>> Handle(CancellationToken ct) =>
        await db.Regions.AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new RegionResponse(r.Id, r.Name, r.Description))
            .ToListAsync(ct);
}
