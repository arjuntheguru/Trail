using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Events;
using Trail.Domain.Issues;
using Trail.Domain.Notifications;

namespace Trail.Application.Features.Notifications;

/// <summary>Tells the assignee (or the raiser when nobody is assigned) about a comment, unless they wrote it.</summary>
internal sealed class IssueCommentedNotifier(NotificationWriter writer, MembershipDirectory directory, IAppDbContext db)
    : IDomainEventHandler<IssueCommented>
{
    public async Task Handle(IssueCommented domainEvent, CancellationToken ct)
    {
        var issue = await writer.FindIssueAsync(domainEvent.IssueId, ct);
        if (issue is null) return;

        var raiser = issue.AssigneeId is null ? await directory.MembershipOfUserAsync(issue.CreatedById, ct) : null;
        if (NotificationPolicy.CommentRecipient(issue.AssigneeId, raiser) is not { } recipient) return;

        var actor = await directory.MembershipOfUserAsync(domainEvent.ActorId, ct);
        var recipients = NotificationPolicy.Recipients([recipient], actor);
        if (recipients.Count == 0) return;

        var comment = (await db.IssueEvents.FindAsync([domainEvent.EventId], ct))?.Note;
        var text = NotificationText.IssueCommented(issue.Number, issue.Title, await writer.SiteNameAsync(issue.SiteId, ct), comment);
        await writer.AddAsync(
            recipients, NotificationKind.IssueCommented, NotificationSubjectType.Issue, issue.Id, issue.SiteId, text, ct);
    }
}
