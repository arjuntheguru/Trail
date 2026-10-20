using Trail.Domain.Issues;

namespace Trail.Application.Features.Reports;

public static class ActivityKinds
{
    public const string TaskCompleted = "task_completed";
    public const string TaskApproved = "task_approved";
    public const string IssueRaised = "issue_raised";
    public const string IssueStatusChanged = "issue_status_changed";
    public const string IssueAssigned = "issue_assigned";
    public const string IssueCommented = "issue_commented";

    /// <summary>The feed kind of an issue timeline event, or null for events the feed leaves out (priority and due changes).</summary>
    public static string? FromIssueEvent(IssueEventKind kind) => kind switch
    {
        IssueEventKind.Raised => IssueRaised,
        IssueEventKind.StatusChanged => IssueStatusChanged,
        IssueEventKind.Assigned => IssueAssigned,
        IssueEventKind.Comment => IssueCommented,
        _ => null,
    };
}

internal sealed record ActivityRow(
    string Kind, DateTime OccurredAt, Guid SiteId, Guid? ActorId, Guid SubjectId, string Title, int? IssueNumber);

internal static class ActivityMerger
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    public static int ClampLimit(int? limit) => Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

    /// <summary>
    /// Newest first across all sources. More than <paramref name="limit"/> rows means another page exists, and
    /// NextBefore is then the timestamp of the last row returned (null otherwise).
    /// </summary>
    public static (IReadOnlyList<ActivityRow> Items, DateTime? NextBefore) Merge(IEnumerable<ActivityRow> rows, int limit)
    {
        var ordered = rows
            .OrderByDescending(r => r.OccurredAt)
            .ThenBy(r => r.Kind, StringComparer.Ordinal)
            .ThenBy(r => r.SubjectId)
            .ToList();
        var page = ordered.Take(limit).ToList();
        return (page, ordered.Count > limit ? page[^1].OccurredAt : null);
    }
}
