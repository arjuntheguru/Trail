using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Paging;

namespace Trail.Application.Features.Sites;

public sealed record ListSitesQuery(string? Search = null, Guid? RegionId = null, bool? IsActive = null);

public sealed class ListSitesHandler(IAppDbContext db, ISiteAccess access)
{
    public async Task<PagedResult<SiteResponse>> Handle(ListSitesQuery query, PageRequest page, CancellationToken ct)
    {
        var scope = await access.GetScopeAsync(ct);
        var sites = access.ApplyTo(db.Sites.AsNoTracking(), scope);

        if (query.RegionId is { } regionId) sites = sites.Where(s => s.RegionId == regionId);
        if (query.IsActive is { } isActive) sites = sites.Where(s => s.IsActive == isActive);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            sites = sites.Where(s => s.Name.Contains(term) || (s.Code != null && s.Code.Contains(term)));
        }

        var paged = await sites
            .OrderBy(s => s.Name).ThenBy(s => s.Id)
            .Select(s => new SiteRow(s, s.Region == null ? null : s.Region.Name))
            .ToPagedResultAsync(page, ct);

        return new PagedResult<SiteResponse>(
            paged.Items.Select(r => SiteMapping.ToResponse(r.Site, r.RegionName)).ToList(),
            paged.Page, paged.PageSize, paged.TotalCount);
    }

    private sealed record SiteRow(Domain.Organizations.Site Site, string? RegionName);
}
