using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Application.Features.Issues;
using Trail.Application.Features.Tasks;
using Trail.Domain.Issues;

namespace Trail.Application.Features.Reports;

/// <summary>Before is an exclusive upper bound on the timestamp (use the previous page's NextBefore); Limit defaults to 50, max 200.</summary>
public sealed record ActivityFeedQuery(Guid? SiteId = null, DateTime? Before = null, int? Limit = null);

public sealed class GetActivityFeedHandler(IAppDbContext db, ISiteAccess access)
{
    public async Task<Result<ActivityFeedResponse>> Handle(ActivityFeedQuery query, CancellationToken ct)
    {
        var limit = ActivityMerger.ClampLimit(query.Limit);
        var take = limit + 1; // one extra row per source tells us whether another page exists
        var before = query.Before is { } b ? IssueRules.AsUtc(b) : (DateTime?)null;

        var scope = await access.GetScopeAsync(ct);
        if (await ReportScope.CheckSiteAsync(db, scope, query.SiteId, ct) is { } error) return error;

        var tasks = db.SiteTasks.AsNoTracking().InScope(scope);
        var issues = IssueRules.InScope(db.Issues.AsNoTracking(), scope);
        if (query.SiteId is { } siteId)
        {
            tasks = tasks.Where(t => t.SiteId == siteId);
            issues = issues.Where(i => i.SiteId == siteId);
        }

        var completed = await tasks
            .Where(t => t.CompletedAt != null && (before == null || t.CompletedAt < before))
            .OrderByDescending(t => t.CompletedAt).ThenBy(t => t.Id)
            .Select(t => new { t.Id, t.SiteId, t.Title, At = t.CompletedAt!.Value, Actor = t.CompletedById })
            .Take(take).ToListAsync(ct);

        var approved = await tasks
            .Where(t => t.ApprovedAt != null && (before == null || t.ApprovedAt < before))
            .OrderByDescending(t => t.ApprovedAt).ThenBy(t => t.Id)
            .Select(t => new { t.Id, t.SiteId, t.Title, At = t.ApprovedAt!.Value, Actor = t.ApprovedById })
            .Take(take).ToListAsync(ct);

        var events = await db.IssueEvents.AsNoTracking()
            .Join(issues, e => e.IssueId, i => i.Id, (e, i) => new
            {
                e.Kind, e.OccurredAt, e.ActorId, IssueId = i.Id, i.SiteId, i.Number, i.Title, e.Id,
            })
            .Where(e => (e.Kind == IssueEventKind.Raised || e.Kind == IssueEventKind.StatusChanged
                         || e.Kind == IssueEventKind.Assigned || e.Kind == IssueEventKind.Comment)
                        && (before == null || e.OccurredAt < before))
            .OrderByDescending(e => e.OccurredAt).ThenBy(e => e.Id)
            .Take(take).ToListAsync(ct);

        var rows = completed.Select(t => new ActivityRow(ActivityKinds.TaskCompleted, t.At, t.SiteId, t.Actor, t.Id, t.Title, null))
            .Concat(approved.Select(t => new ActivityRow(ActivityKinds.TaskApproved, t.At, t.SiteId, t.Actor, t.Id, t.Title, null)))
            .Concat(events.Select(e => new ActivityRow(
                ActivityKinds.FromIssueEvent(e.Kind)!, e.OccurredAt, e.SiteId, e.ActorId, e.IssueId, e.Title, e.Number)));

        var (items, nextBefore) = ActivityMerger.Merge(rows, limit);

        // Two small lookups for the names on this page, not one per row.
        var siteIds = items.Select(i => i.SiteId).Distinct().ToList();
        var siteNames = await db.Sites.AsNoTracking().IgnoreQueryFilters([QueryFilters.SoftDelete])
            .Where(s => siteIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var userNames = await ReportScope.UserNamesAsync(db, items.Select(i => i.ActorId), ct);

        var response = items.Select(i => new ActivityItemResponse(
            i.Kind, i.OccurredAt, i.SiteId, siteNames.GetValueOrDefault(i.SiteId),
            i.ActorId, i.ActorId is { } a ? userNames.GetValueOrDefault(a) : null,
            i.SubjectId, i.Title, i.IssueNumber)).ToList();
        return new ActivityFeedResponse(response, nextBefore);
    }
}
