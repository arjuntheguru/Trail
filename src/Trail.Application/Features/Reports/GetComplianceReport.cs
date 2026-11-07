using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Application.Features.Tasks;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Reports;

/// <summary>From/To bound the tasks' due date (inclusive) and span at most 93 days.</summary>
public sealed record ComplianceReportQuery(
    DateOnly? From = null, DateOnly? To = null, Guid? SiteId = null, ComplianceGrouping? GroupBy = null);

public sealed class GetComplianceReportHandler(IAppDbContext db, ISiteAccess access, TimeProvider clock)
{
    public async Task<Result<ComplianceReportResponse>> Handle(ComplianceReportQuery query, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var range = ReportRange.Resolve(query.From, query.To, DateOnly.FromDateTime(now));
        if (range.IsFailure) return range.Error!;
        var (from, to) = range.Value;

        var scope = await access.GetScopeAsync(ct);
        if (await ReportScope.CheckSiteAsync(db, scope, query.SiteId, ct) is { } error) return error;

        var tasks = db.SiteTasks.AsNoTracking().InScope(scope)
            .Where(t => t.DueDate >= from && t.DueDate <= to && t.Status != SiteTaskStatus.Cancelled);
        if (query.SiteId is { } siteId) tasks = tasks.Where(t => t.SiteId == siteId);

        // One grouped query; the calculator applies the per-site "today" rule, which needs each site's time zone.
        var rows = await tasks
            .GroupBy(t => new
            {
                t.SiteId, t.TemplateId, t.DueDate, t.Status, t.CompletedLate, t.HasFailures,
                AwaitingApproval = t.Status == SiteTaskStatus.Completed && t.RequiresApproval && t.ApprovedAt == null,
            })
            .Select(g => new { g.Key, Title = g.Max(t => t.Title), Count = g.Count() })
            .ToListAsync(ct);

        var buckets = rows.Select(r => new ComplianceBucket(
            r.Key.SiteId, r.Key.TemplateId, r.Title ?? "", r.Key.DueDate, r.Key.Status,
            r.Key.CompletedLate, r.Key.HasFailures, r.Key.AwaitingApproval, r.Count)).ToList();

        var sites = (await ReportScope.LoadSitesAsync(db, access, scope, query.SiteId, now, activeOnly: false, ct))
            .ToDictionary(s => s.Id);

        var groupBy = query.GroupBy ?? ComplianceGrouping.Site;
        var (groups, total) = ComplianceCalculator.Compute(buckets, groupBy, sites);
        return new ComplianceReportResponse(from, to, groupBy, groups, total);
    }
}
