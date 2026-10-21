using Trail.Application.Common.Events;
using Trail.Application.Common.Security;
using Trail.Domain.Issues;
using Trail.Domain.Notifications;

namespace Trail.Application.Features.Notifications;

/// <summary>
/// Tells the site's issue managers about a new issue when <see cref="NotificationPolicy.NotifyManagersOfRaise"/> says so.
/// An assignee is left to <see cref="IssueAssignedNotifier"/> so nobody is told twice.
/// </summary>
internal sealed class IssueRaisedNotifier(NotificationWriter writer, MembershipDirectory directory)
    : IDomainEventHandler<IssueRaised>
{
    public async Task Handle(IssueRaised domainEvent, CancellationToken ct)
    {
        var issue = await writer.FindIssueAsync(domainEvent.IssueId, ct);
        if (issue is null) return;
        if (!NotificationPolicy.NotifyManagersOfRaise(issue.Priority, issue.Source, issue.AssigneeId is not null)) return;

        var managers = await directory.HoldingAtSiteAsync(issue.SiteId, Permissions.Issues.Manage, ct);
        var actor = await directory.MembershipOfUserAsync(domainEvent.ActorId, ct);
        var recipients = NotificationPolicy.Recipients(managers.Select(m => m.MembershipId), actor, issue.AssigneeId);
        if (recipients.Count == 0) return;

        var text = NotificationText.IssueRaised(issue.Number, issue.Title, await writer.SiteNameAsync(issue.SiteId, ct), issue.Priority);
        await writer.AddAsync(recipients, NotificationKind.IssueRaised, NotificationSubjectType.Issue, issue.Id, issue.SiteId, text, ct);
    }
}
