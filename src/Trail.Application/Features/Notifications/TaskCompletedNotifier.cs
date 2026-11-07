using Trail.Application.Common.Events;
using Trail.Application.Common.Security;
using Trail.Domain.Notifications;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Notifications;

/// <summary>When a completed task needs sign-off, tells the people at its site who can approve it.</summary>
internal sealed class TaskCompletedNotifier(NotificationWriter writer, MembershipDirectory directory)
    : IDomainEventHandler<TaskCompleted>
{
    public async Task Handle(TaskCompleted domainEvent, CancellationToken ct)
    {
        var task = await writer.FindTaskAsync(domainEvent.TaskId, ct);
        if (task is not { IsAwaitingApproval: true }) return;

        var approvers = await directory.HoldingAtSiteAsync(task.SiteId, Permissions.Tasks.Approve, ct);
        var actor = await directory.MembershipOfUserAsync(domainEvent.ActorId, ct);
        var recipients = NotificationPolicy.Recipients(approvers.Select(m => m.MembershipId), actor);
        if (recipients.Count == 0) return;

        var text = NotificationText.TaskAwaitingApproval(task.Title, await writer.SiteNameAsync(task.SiteId, ct), task.DueDate);
        await writer.AddAsync(
            recipients, NotificationKind.TaskAwaitingApproval, NotificationSubjectType.Task, task.Id, task.SiteId, text, ct);
    }
}
