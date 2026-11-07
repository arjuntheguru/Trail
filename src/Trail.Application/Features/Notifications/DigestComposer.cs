using System.Text;
using Trail.Application.Common.Abstractions;

namespace Trail.Application.Features.Notifications;

/// <summary>Renders the daily digest email. Pure.</summary>
public static class DigestComposer
{
    public const int MaxItemsPerSection = 15;

    /// <returns>The email, or null when there is nothing to report (no email is sent then).</returns>
    public static EmailMessage? Compose(
        string toAddress, string firstName, string organizationName, DateOnly localToday,
        DigestContent content, INotificationLinks links)
    {
        if (content.IsEmpty) return null;

        var text = new StringBuilder();
        text.Append("Hi ").Append(firstName).Append(",\n\nHere is your daily summary for ").Append(organizationName).Append(".\n");

        if (content.MyOpenIssues.Count > 0)
        {
            Section(text, "Open issues assigned to you", content.MyOpenIssues.Count);
            foreach (var issue in content.MyOpenIssues.Take(MaxItemsPerSection)) Line(text, issue, links, withSite: true);
            More(text, content.MyOpenIssues.Count);
        }

        if (content.AwaitingApproval.Count > 0)
        {
            Section(text, "Tasks waiting for your approval", content.AwaitingApproval.Count);
            foreach (var task in content.AwaitingApproval.Take(MaxItemsPerSection))
                text.Append("  - ").Append(task.Title).Append(" (").Append(task.SiteName).Append(", due ")
                    .Append(task.DueDate.ToString("yyyy-MM-dd")).Append("): ").Append(links.Task(task.TaskId)).Append('\n');
            More(text, content.AwaitingApproval.Count);
        }

        if (content.OverdueIssues.Count > 0)
        {
            Section(text, "Overdue issues at your sites", content.OverdueIssues.Count);
            foreach (var group in content.OverdueIssues.GroupBy(i => i.SiteName).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                text.Append("  ").Append(group.Key).Append('\n');
                foreach (var issue in group.Take(MaxItemsPerSection)) Line(text, issue, links, withSite: false);
                if (group.Count() > MaxItemsPerSection)
                    text.Append("    ...and ").Append(group.Count() - MaxItemsPerSection).Append(" more\n");
            }
        }

        if (content.MissedTasks.Count > 0)
        {
            Section(text, "Tasks missed yesterday", content.MissedTasks.Count);
            foreach (var group in content.MissedTasks.GroupBy(t => t.SiteName).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                text.Append("  ").Append(group.Key).Append(" (").Append(group.Count()).Append(")\n");
                foreach (var task in group.Take(MaxItemsPerSection)) text.Append("    - ").Append(task.Title).Append('\n');
                if (group.Count() > MaxItemsPerSection)
                    text.Append("    ...and ").Append(group.Count() - MaxItemsPerSection).Append(" more\n");
            }
        }

        text.Append("\nYou can turn this summary off in your notification settings:\n").Append(links.NotificationSettings());
        return new EmailMessage(toAddress, $"Your Trail summary for {localToday:yyyy-MM-dd}", text.ToString());
    }

    private static void Section(StringBuilder text, string heading, int count) =>
        text.Append('\n').Append(heading).Append(" (").Append(count).Append(")\n");

    private static void More(StringBuilder text, int count)
    {
        if (count > MaxItemsPerSection) text.Append("  ...and ").Append(count - MaxItemsPerSection).Append(" more\n");
    }

    private static void Line(StringBuilder text, DigestIssueItem issue, INotificationLinks links, bool withSite)
    {
        text.Append(withSite ? "  - #" : "    - #").Append(issue.Number).Append(' ').Append(issue.Title)
            .Append(" [").Append(issue.Priority).Append(']');
        if (withSite) text.Append(" at ").Append(issue.SiteName);
        if (issue.DueAt is { } due) text.Append(", due ").Append(due.ToString("yyyy-MM-dd HH:mm")).Append(" UTC");
        text.Append(": ").Append(links.Issue(issue.IssueId)).Append('\n');
    }
}
