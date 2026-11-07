using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;

namespace Trail.Application.Features.Sites;

public sealed class GetSiteHandler(IAppDbContext db, ISiteAccess access)
{
    public async Task<Result<SiteResponse>> Handle(Guid id, CancellationToken ct)
    {
        var scope = await access.GetScopeAsync(ct);
        var site = await access.ApplyTo(db.Sites.AsNoTracking(), scope)
            .Where(s => s.Id == id)
            .Select(s => new { Site = s, RegionName = s.Region == null ? null : s.Region.Name })
            .FirstOrDefaultAsync(ct);

        // Sites outside the caller's scope look exactly like sites that do not exist.
        return site is null
            ? Error.NotFound("site.not_found", "Site not found.")
            : SiteMapping.ToResponse(site.Site, site.RegionName);
    }
}
