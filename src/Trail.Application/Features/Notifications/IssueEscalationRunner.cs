using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Security;
using Trail.Domain.Issues;
using Trail.Domain.Notifications;

namespace Trail.Application.Features.Notifications;

/// <summary>
/// Announces overdue issues of the current tenant and escalates the ones that stay overdue. Idempotent through
/// <see cref="Issue.OverdueNotifiedAt"/> and <see cref="Issue.EscalatedAt"/>; when two instances race, the issue
/// row's concurrency stamp makes one save fail as a whole, notifications included.
/// </summary>
public sealed class IssueEscalationRunner(
    IAppDbContext db, NotificationWriter writer, MembershipDirectory directory, TimeProvider clock)
{
    public const int BatchSize = 200;

    /// <returns>The number of overdue notices and escalations made.</returns>
    public async Task<(int Overdue, int Escalated)> RunAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var issues = await db.Issues
            .Where(i => (i.Status == IssueStatus.Open || i.Status == IssueStatus.InProgress)
                        && i.DueAt != null && i.DueAt < now
                        && (i.OverdueNotifiedAt == null || i.EscalatedAt == null))
            .OrderBy(i => i.DueAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        var managersBySite = new Dictionary<Guid, IReadOnlyList<SiteMember>>();
        var siteNames = new Dictionary<Guid, string?>();
        int overdue = 0, escalated = 0;

        foreach (var issue in issues)
        {
            var decision = EscalationPolicy.Decide(issue.Status, issue.Priority, issue.DueAt, issue.OverdueNotifiedAt, issue.EscalatedAt, now);
            if (decision == EscalationDecision.Nothing) continue;

            if (!managersBySite.TryGetValue(issue.SiteId, out var managers))
                managersBySite[issue.SiteId] = managers = await directory.HoldingAtSiteAsync(issue.SiteId, Permissions.Issues.Manage, ct);
            if (!siteNames.TryGetValue(issue.SiteId, out var siteName))
                siteNames[issue.SiteId] = siteName = await writer.SiteNameAsync(issue.SiteId, ct);
            var dueAt = issue.DueAt!.Value;

            if (decision.NotifyOverdue)
            {
                await writer.AddAsync(
                    EscalationPolicy.OverdueRecipients(issue.AssigneeId, managers), NotificationKind.IssueOverdue,
                    NotificationSubjectType.Issue, issue.Id, issue.SiteId,
                    NotificationText.IssueOverdue(issue.Number, issue.Title, siteName, dueAt), ct);
                issue.MarkOverdueNotified(now);
                overdue++;
            }

            if (decision.Escalate)
            {
                await writer.AddAsync(
                    EscalationPolicy.EscalationRecipients(managers), NotificationKind.IssueEscalated,
                    NotificationSubjectType.Issue, issue.Id, issue.SiteId,
                    NotificationText.IssueEscalated(issue.Number, issue.Title, siteName, dueAt), ct);
                issue.MarkEscalated(now);
                escalated++;
            }
        }

        if (overdue + escalated > 0) await db.SaveChangesAsync(ct);
        return (overdue, escalated);
    }
}
