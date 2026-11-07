using Trail.Application.Common.Events;
using Trail.Application.Common.Security;
using Trail.Domain.Issues;
using Trail.Domain.Notifications;

namespace Trail.Application.Features.Notifications;

/// <summary>Resolved issues go to the managers who verify them; reopened and closed ones to the assignee.</summary>
internal sealed class IssueStatusChangedNotifier(NotificationWriter writer, MembershipDirectory directory)
    : IDomainEventHandler<IssueStatusChanged>
{
    public async Task Handle(IssueStatusChanged domainEvent, CancellationToken ct)
    {
        var audience = NotificationPolicy.AudienceForStatusChange(domainEvent.From, domainEvent.To);
        if (audience == StatusChangeAudience.None) return;

        var issue = await writer.FindIssueAsync(domainEvent.IssueId, ct);
        if (issue is null) return;

        IEnumerable<Guid> candidates = audience == StatusChangeAudience.Assignee
            ? issue.AssigneeId is { } assignee ? [assignee] : []
            : (await directory.HoldingAtSiteAsync(issue.SiteId, Permissions.Issues.Manage, ct)).Select(m => m.MembershipId);

        var actor = await directory.MembershipOfUserAsync(domainEvent.ActorId, ct);
        var recipients = NotificationPolicy.Recipients(candidates, actor);
        if (recipients.Count == 0) return;

        var verb = NotificationPolicy.StatusChangeVerb(domainEvent.From, domainEvent.To);
        var text = NotificationText.IssueStatusChanged(
            issue.Number, issue.Title, await writer.SiteNameAsync(issue.SiteId, ct), verb, domainEvent.Note);
        await writer.AddAsync(
            recipients, NotificationKind.IssueStatusChanged, NotificationSubjectType.Issue, issue.Id, issue.SiteId, text, ct);
    }
}
