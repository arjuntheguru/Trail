using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Application.Features.Issues;
using Trail.Application.Features.Tasks;
using Trail.Domain.Issues;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Reports;

public sealed record DashboardQuery(Guid? SiteId = null);

/// <summary>Today's position at each active site in scope, where "today" is the site's own local date.</summary>
public sealed class GetDashboardHandler(IAppDbContext db, ISiteAccess access, TimeProvider clock)
{
    public async Task<Result<DashboardResponse>> Handle(DashboardQuery query, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var scope = await access.GetScopeAsync(ct);
        if (await ReportScope.CheckSiteAsync(db, scope, query.SiteId, ct) is { } error) return error;

        var sites = await ReportScope.LoadSitesAsync(db, access, scope, query.SiteId, now, activeOnly: true, ct);
        if (sites.Count == 0)
            return DashboardCalculator.Compute(sites, [], []);

        var siteIds = sites.Select(s => s.Id).ToList();
        var earliest = sites.Min(s => s.Today).AddDays(-1);
        var latest = sites.Max(s => s.Today);

        // Tasks: yesterday and today across every site in one grouped query; the calculator picks each site's own days.
        var taskRows = await db.SiteTasks.AsNoTracking().InScope(scope)
            .Where(t => siteIds.Contains(t.SiteId) && t.DueDate >= earliest && t.DueDate <= latest
                        && t.Status != SiteTaskStatus.Cancelled)
            .GroupBy(t => new
            {
                t.SiteId, t.DueDate, t.Status, t.CompletedLate,
                Overdue = t.Status == SiteTaskStatus.Open && t.DueAt < now,
            })
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var issueRows = await IssueRules.InScope(db.Issues.AsNoTracking(), scope)
            .Where(i => siteIds.Contains(i.SiteId) && (i.Status == IssueStatus.Open || i.Status == IssueStatus.InProgress))
            .GroupBy(i => new { i.SiteId, i.Priority, Overdue = i.DueAt != null && i.DueAt < now })
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return DashboardCalculator.Compute(
            sites,
            taskRows.Select(r => new DashboardTaskBucket(r.Key.SiteId, r.Key.DueDate, r.Key.Status, r.Key.CompletedLate, r.Key.Overdue, r.Count)),
            issueRows.Select(r => new DashboardIssueBucket(r.Key.SiteId, r.Key.Priority, r.Key.Overdue, r.Count)));
    }
}
