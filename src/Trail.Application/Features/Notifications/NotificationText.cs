using Trail.Domain.Issues;
using Trail.Domain.Notifications;

namespace Trail.Application.Features.Notifications;

/// <summary>Wording of in-app notifications (and so of the emails built from them). Pure.</summary>
public static class NotificationText
{
    public static (string Title, string Body) IssueAssigned(int number, string issueTitle, string? siteName) =>
        ($"Issue #{number} was assigned to you", Describe(issueTitle, siteName));

    public static (string Title, string Body) IssueRaised(int number, string issueTitle, string? siteName, IssuePriority priority) =>
        ($"New {priority.ToString().ToLowerInvariant()} priority issue #{number}", Describe(issueTitle, siteName));

    public static (string Title, string Body) IssueStatusChanged(int number, string issueTitle, string? siteName, string verb, string? note) =>
        ($"Issue #{number} was {verb}", Describe(issueTitle, siteName, note));

    public static (string Title, string Body) IssueCommented(int number, string issueTitle, string? siteName, string? comment) =>
        ($"New comment on issue #{number}", Describe(issueTitle, siteName, comment));

    public static (string Title, string Body) IssueOverdue(int number, string issueTitle, string? siteName, DateTime dueAt) =>
        ($"Issue #{number} is overdue", Describe(issueTitle, siteName, $"It was due {dueAt:yyyy-MM-dd HH:mm} UTC."));

    public static (string Title, string Body) IssueEscalated(int number, string issueTitle, string? siteName, DateTime dueAt) =>
        ($"Issue #{number} needs attention", Describe(issueTitle, siteName, $"It has been overdue since {dueAt:yyyy-MM-dd HH:mm} UTC and was escalated."));

    public static (string Title, string Body) TaskAwaitingApproval(string taskTitle, string? siteName, DateOnly dueDate) =>
        ($"\"{Short(taskTitle)}\" is waiting for your approval", Describe(taskTitle, siteName, $"Due {dueDate:yyyy-MM-dd}."));

    private static string Describe(string title, string? siteName, string? detail = null)
    {
        var text = string.IsNullOrWhiteSpace(siteName) ? title : $"{title} at {siteName}";
        if (!string.IsNullOrWhiteSpace(detail)) text += $"\n{detail.Trim()}";
        return IssuePolicy.Truncate(text, Notification.BodyMaxLength);
    }

    private static string Short(string text) => IssuePolicy.Truncate(text, 120);
}
