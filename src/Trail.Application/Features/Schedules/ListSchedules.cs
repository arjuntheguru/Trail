using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Paging;

namespace Trail.Application.Features.Schedules;

public sealed record ListSchedulesQuery(Guid? TemplateId = null, Guid? SiteId = null, bool IncludeInactive = false);

public sealed class ListSchedulesHandler(IAppDbContext db, ISiteAccess access)
{
    public async Task<PagedResult<ScheduleResponse>> Handle(ListSchedulesQuery query, PageRequest page, CancellationToken ct)
    {
        var scope = await access.GetScopeAsync(ct);
        var schedules = db.TaskSchedules.AsNoTracking();

        if (!scope.AllSites)
        {
            // Only schedules whose every target is inside the caller's scope; all-sites ones never are.
            var siteIds = scope.SiteIds.ToList();
            var regionIds = scope.RegionIds.ToList();
            schedules = schedules.Where(s => !s.AllSites
                && s.Sites.All(x => siteIds.Contains(x.SiteId))
                && s.Regions.All(x => regionIds.Contains(x.RegionId)));
        }

        if (!query.IncludeInactive) schedules = schedules.Where(s => s.IsActive);
        if (query.TemplateId is { } templateId) schedules = schedules.Where(s => s.TemplateId == templateId);
        if (query.SiteId is { } siteId)
        {
            var regionId = await db.Sites.Where(s => s.Id == siteId).Select(s => s.RegionId).FirstOrDefaultAsync(ct);
            schedules = schedules.Where(s => s.AllSites
                || s.Sites.Any(x => x.SiteId == siteId)
                || (regionId != null && s.Regions.Any(x => x.RegionId == regionId)));
        }

        var paged = await schedules
            .Include(s => s.Template).Include(s => s.Sites).Include(s => s.Regions)
            .OrderBy(s => s.Template.Name).ThenBy(s => s.DueTime).ThenBy(s => s.Id)
            .ToPagedResultAsync(page, ct);

        return new PagedResult<ScheduleResponse>(
            paged.Items.Select(ScheduleMapping.ToResponse).ToList(), paged.Page, paged.PageSize, paged.TotalCount);
    }
}
