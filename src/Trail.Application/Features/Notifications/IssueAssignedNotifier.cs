using Trail.Application.Common.Events;
using Trail.Domain.Issues;
using Trail.Domain.Notifications;

namespace Trail.Application.Features.Notifications;

/// <summary>Tells the new assignee, unless they assigned the issue to themselves.</summary>
internal sealed class IssueAssignedNotifier(NotificationWriter writer, MembershipDirectory directory)
    : IDomainEventHandler<IssueAssigned>
{
    public async Task Handle(IssueAssigned domainEvent, CancellationToken ct)
    {
        if (domainEvent.AssigneeId is not { } assigneeId) return;
        var issue = await writer.FindIssueAsync(domainEvent.IssueId, ct);
        if (issue is null) return;

        var actor = await directory.MembershipOfUserAsync(domainEvent.ActorId, ct);
        var recipients = NotificationPolicy.Recipients([assigneeId], actor);
        if (recipients.Count == 0) return;

        var text = NotificationText.IssueAssigned(issue.Number, issue.Title, await writer.SiteNameAsync(issue.SiteId, ct));
        await writer.AddAsync(recipients, NotificationKind.IssueAssigned, NotificationSubjectType.Issue, issue.Id, issue.SiteId, text, ct);
    }
}
