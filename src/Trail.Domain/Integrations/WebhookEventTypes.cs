namespace Trail.Domain.Integrations;

/// <summary>The stable event names webhook subscribers filter on. Renaming one is a breaking change for customers.</summary>
public static class WebhookEventTypes
{
    public const string IssueRaised = "issue.raised";
    public const string IssueAssigned = "issue.assigned";
    public const string IssueStatusChanged = "issue.status_changed";
    public const string IssueCommented = "issue.commented";
    public const string TaskCompleted = "task.completed";
    public const string TaskApproved = "task.approved";

    /// <summary>Sent by the "test" action only; it is not part of the catalog and cannot be subscribed to.</summary>
    public const string Ping = "ping";

    public static IReadOnlyList<string> All { get; } =
        [IssueRaised, IssueAssigned, IssueStatusChanged, IssueCommented, TaskCompleted, TaskApproved];

    public static bool IsKnown(string eventType) => All.Contains(eventType, StringComparer.Ordinal);
}
