using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Paging;
using Trail.Domain.Issues;

namespace Trail.Application.Features.Issues;

public sealed record ListIssuesQuery(
    IssueStatus[]? Status = null,
    Guid? SiteId = null,
    IssuePriority? Priority = null,
    Guid? AssigneeId = null,
    bool? Mine = null,
    bool? Overdue = null,
    IssueSource? Source = null,
    Guid? SiteTaskId = null,
    string? Search = null,
    DateTime? From = null,
    DateTime? To = null);

public sealed partial class ListIssuesHandler(IAppDbContext db, ISiteAccess access, ICurrentUser caller, TimeProvider clock)
{
    public async Task<PagedResult<IssueListItemResponse>> Handle(ListIssuesQuery query, PageRequest page, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var issues = IssueRules.InScope(db.Issues.AsNoTracking(), await access.GetScopeAsync(ct));

        if (query.Status is { Length: > 0 } statuses) issues = issues.Where(i => statuses.Contains(i.Status));
        if (query.SiteId is { } siteId) issues = issues.Where(i => i.SiteId == siteId);
        if (query.Priority is { } priority) issues = issues.Where(i => i.Priority == priority);
        if (query.AssigneeId is { } assigneeId) issues = issues.Where(i => i.AssigneeId == assigneeId);
        if (query.Mine == true)
        {
            var mine = caller.MembershipId;
            issues = issues.Where(i => i.AssigneeId != null && i.AssigneeId == mine);
        }
        if (query.Overdue == true)
            issues = issues.Where(i => (i.Status == IssueStatus.Open || i.Status == IssueStatus.InProgress)
                                       && i.DueAt != null && i.DueAt < now);
        if (query.Source is { } source) issues = issues.Where(i => i.Source == source);
        if (query.SiteTaskId is { } taskId) issues = issues.Where(i => i.SiteTaskId == taskId);
        if (query.From is { } from)
        {
            var fromUtc = IssueRules.AsUtc(from);
            issues = issues.Where(i => i.CreatedAt >= fromUtc);
        }
        if (query.To is { } to)
        {
            var toUtc = IssueRules.AsUtc(to);
            issues = issues.Where(i => i.CreatedAt <= toUtc);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            var numberMatch = NumberPattern().Match(term);
            if (numberMatch.Success && int.TryParse(numberMatch.Groups[1].Value, out var number))
                issues = issues.Where(i => i.Number == number || i.Title.Contains(term));
            else
                issues = issues.Where(i => i.Title.Contains(term));
        }

        var sites = db.Sites.IgnoreQueryFilters([QueryFilters.SoftDelete]);

        // Priority is stored by name, so rank it explicitly rather than sorting the text.
        // Active first, then most urgent, then soonest due (undated last), then newest.
        var paged = await issues
            .OrderBy(i => i.Status == IssueStatus.Open || i.Status == IssueStatus.InProgress ? 0 : 1)
            .ThenByDescending(i => i.Priority == IssuePriority.Critical ? 3
                : i.Priority == IssuePriority.High ? 2
                : i.Priority == IssuePriority.Medium ? 1 : 0)
            .ThenBy(i => i.DueAt == null ? 1 : 0)
            .ThenBy(i => i.DueAt)
            .ThenByDescending(i => i.Number)
            .Select(i => new Row(
                i.Id, i.Number, i.Title, i.SiteId,
                sites.Where(s => s.Id == i.SiteId).Select(s => s.Name).FirstOrDefault(),
                i.Priority, i.Status, i.Source, i.AssigneeId,
                db.Memberships.Where(m => m.Id == i.AssigneeId).Select(m => m.User.FirstName + " " + m.User.LastName).FirstOrDefault(),
                i.DueAt, i.CreatedAt, i.PhotoIds))
            .ToPagedResultAsync(page, ct);

        var items = paged.Items.Select(r => new IssueListItemResponse(
            r.Id, r.Number, r.Title, r.SiteId, r.SiteName, r.Priority, r.Status, r.Source,
            r.AssigneeId is { } a ? new IssueAssigneeResponse(a, r.AssigneeName?.Trim()) : null,
            r.DueAt,
            r.DueAt is { } due && r.Status is IssueStatus.Open or IssueStatus.InProgress && now > due,
            r.CreatedAt, r.PhotoIds.Count)).ToList();
        return new PagedResult<IssueListItemResponse>(items, paged.Page, paged.PageSize, paged.TotalCount);
    }

    [GeneratedRegex(@"^#?(\d{1,9})$")]
    private static partial Regex NumberPattern();

    private sealed record Row(
        Guid Id, int Number, string Title, Guid SiteId, string? SiteName, IssuePriority Priority, IssueStatus Status,
        IssueSource Source, Guid? AssigneeId, string? AssigneeName, DateTime? DueAt, DateTime CreatedAt, List<Guid> PhotoIds);
}
