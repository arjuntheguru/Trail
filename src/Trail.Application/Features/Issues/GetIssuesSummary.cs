using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Domain.Issues;

namespace Trail.Application.Features.Issues;

public sealed record GetIssuesSummaryQuery(Guid? SiteId = null);

public sealed class GetIssuesSummaryHandler(IAppDbContext db, ISiteAccess access, ICurrentUser caller, TimeProvider clock)
{
    public async Task<IssueSummaryResponse> Handle(GetIssuesSummaryQuery query, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var issues = IssueRules.InScope(db.Issues.AsNoTracking(), await access.GetScopeAsync(ct));
        if (query.SiteId is { } siteId) issues = issues.Where(i => i.SiteId == siteId);

        var byStatus = await issues.GroupBy(i => i.Status)
            .Select(g => new { Key = g.Key, Count = g.Count() }).ToListAsync(ct);

        var active = issues.Where(i => i.Status == IssueStatus.Open || i.Status == IssueStatus.InProgress);
        var byPriority = await active.GroupBy(i => i.Priority)
            .Select(g => new { Key = g.Key, Count = g.Count() }).ToListAsync(ct);
        var overdue = await active.CountAsync(i => i.DueAt != null && i.DueAt < now, ct);

        var mine = caller.MembershipId;
        var assignedToMe = mine is null ? 0 : await active.CountAsync(i => i.AssigneeId == mine, ct);

        return new IssueSummaryResponse(
            Enum.GetValues<IssueStatus>().ToDictionary(s => s, s => byStatus.FirstOrDefault(x => x.Key == s)?.Count ?? 0),
            overdue,
            Enum.GetValues<IssuePriority>().ToDictionary(p => p, p => byPriority.FirstOrDefault(x => x.Key == p)?.Count ?? 0),
            assignedToMe);
    }
}
