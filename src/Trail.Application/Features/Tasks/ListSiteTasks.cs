using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Common;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Tasks;

public sealed record ListSiteTasksQuery(DateOnly? Date = null, Guid? DepartmentId = null);

public sealed class ListSiteTasksHandler(IAppDbContext db, ISiteAccess access, TimeProvider clock)
{
    public async Task<Result<SiteTaskListResponse>> Handle(Guid siteId, ListSiteTasksQuery query, CancellationToken ct)
    {
        var scope = await access.GetScopeAsync(ct);
        var site = await access.ApplyTo(db.Sites.AsNoTracking(), scope)
            .Where(s => s.Id == siteId)
            .Select(s => new { s.Name, s.TimeZone })
            .FirstOrDefaultAsync(ct);
        if (site is null) return Error.NotFound("site.not_found", "Site not found.");

        var now = clock.GetUtcNow().UtcDateTime;
        var today = SiteTime.Today(now, SiteTime.Zone(site.TimeZone));
        var date = query.Date ?? today;

        var tasks = db.SiteTasks.AsNoTracking().Where(t => t.SiteId == siteId);
        if (query.DepartmentId is { } departmentId) tasks = tasks.Where(t => t.DepartmentId == departmentId);

        // Today's list also carries earlier days' missed tasks that can still be completed late.
        var lateCutoff = now - SiteTask.LateCompletionWindow;
        tasks = date == today
            ? tasks.Where(t => t.DueDate == date
                               || (t.Status == SiteTaskStatus.Missed && t.DueDate < date && t.DueAt >= lateCutoff))
            : tasks.Where(t => t.DueDate == date);

        var rows = await tasks.OrderBy(t => t.DueAt).ThenBy(t => t.Title).ThenBy(t => t.Id).ToRows().ToListAsync(ct);
        return new SiteTaskListResponse(siteId, site.Name, date, site.TimeZone, await TaskMapping.ToItemsAsync(db, rows, now, ct));
    }
}
