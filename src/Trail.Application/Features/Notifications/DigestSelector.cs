using Trail.Application.Common.Security;
using Trail.Domain.Common;
using Trail.Domain.Issues;

namespace Trail.Application.Features.Notifications;

public sealed record DigestSite(Guid Id, string Name, string TimeZone);

public sealed record DigestTaskRow(Guid Id, string Title, Guid SiteId, DateOnly DueDate);

public sealed record DigestIssueRow(Guid Id, int Number, string Title, Guid SiteId, Guid? AssigneeId, IssuePriority Priority, DateTime? DueAt);

public sealed record DigestTaskItem(Guid TaskId, string Title, string SiteName, DateOnly DueDate);

public sealed record DigestIssueItem(Guid IssueId, int Number, string Title, string SiteName, IssuePriority Priority, DateTime? DueAt);

/// <summary>What goes into one person's digest.</summary>
public sealed record DigestContent(
    IReadOnlyList<DigestTaskItem> MissedTasks,
    IReadOnlyList<DigestTaskItem> AwaitingApproval,
    IReadOnlyList<DigestIssueItem> MyOpenIssues,
    IReadOnlyList<DigestIssueItem> OverdueIssues)
{
    public bool IsEmpty => MissedTasks.Count + AwaitingApproval.Count + MyOpenIssues.Count + OverdueIssues.Count == 0;
}

/// <summary>Picks what belongs in a person's digest from organization-wide rows. Pure.</summary>
public static class DigestSelector
{
    /// <param name="sites">Only the sites this person can reach.</param>
    /// <param name="missedTasks">Missed tasks of any recent day; only each site's local "yesterday" is kept.</param>
    /// <param name="awaitingApproval">Completed tasks still waiting for sign-off.</param>
    /// <param name="activeIssues">Open and in-progress issues.</param>
    public static DigestContent Select(
        Guid membershipId,
        IReadOnlyCollection<string> permissions,
        IReadOnlyDictionary<Guid, DigestSite> sites,
        IEnumerable<DigestTaskRow> missedTasks,
        IEnumerable<DigestTaskRow> awaitingApproval,
        IEnumerable<DigestIssueRow> activeIssues,
        DateTime now)
    {
        var missed = permissions.Contains(Permissions.Tasks.View)
            ? missedTasks
                .Where(t => sites.TryGetValue(t.SiteId, out var site)
                            && t.DueDate == SiteTime.Today(now, SiteTime.Zone(site.TimeZone)).AddDays(-1))
                .Select(t => ToItem(t, sites))
                .OrderBy(t => t.SiteName, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : [];

        var approvals = permissions.Contains(Permissions.Tasks.Approve)
            ? awaitingApproval
                .Where(t => sites.ContainsKey(t.SiteId))
                .Select(t => ToItem(t, sites))
                .OrderBy(t => t.DueDate).ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : [];

        var mine = new List<DigestIssueItem>();
        var overdue = new List<DigestIssueItem>();
        if (permissions.Contains(Permissions.Issues.View))
        {
            var ordered = activeIssues
                .OrderByDescending(i => i.Priority).ThenBy(i => i.DueAt ?? DateTime.MaxValue).ThenBy(i => i.Number)
                .ToList();
            mine = ordered.Where(i => i.AssigneeId == membershipId).Select(i => ToItem(i, sites)).ToList();
            // Issues already listed as the person's own are not repeated as overdue ones.
            overdue = ordered
                .Where(i => i.AssigneeId != membershipId && sites.ContainsKey(i.SiteId) && i.DueAt is { } due && due < now)
                .Select(i => ToItem(i, sites))
                .ToList();
        }

        return new DigestContent(missed, approvals, mine, overdue);
    }

    private static DigestTaskItem ToItem(DigestTaskRow task, IReadOnlyDictionary<Guid, DigestSite> sites) =>
        new(task.Id, task.Title, sites[task.SiteId].Name, task.DueDate);

    private static DigestIssueItem ToItem(DigestIssueRow issue, IReadOnlyDictionary<Guid, DigestSite> sites) =>
        new(issue.Id, issue.Number, issue.Title, sites.TryGetValue(issue.SiteId, out var site) ? site.Name : "Unknown site",
            issue.Priority, issue.DueAt);
}
