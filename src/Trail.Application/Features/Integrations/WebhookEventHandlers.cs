using Trail.Application.Common.Events;
using Trail.Domain.Integrations;
using Trail.Domain.Issues;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Integrations;

// One handler per domain event. They only add outbox rows to the save that raised the event.

internal sealed class IssueRaisedWebhookHandler(WebhookOutbox outbox) : IDomainEventHandler<IssueRaised>
{
    public Task Handle(IssueRaised e, CancellationToken ct) =>
        outbox.EnqueueIssueAsync(WebhookEventTypes.IssueRaised, e.IssueId, e.OccurredAt, null, ct);
}

internal sealed class IssueAssignedWebhookHandler(WebhookOutbox outbox) : IDomainEventHandler<IssueAssigned>
{
    public Task Handle(IssueAssigned e, CancellationToken ct) =>
        outbox.EnqueueIssueAsync(WebhookEventTypes.IssueAssigned, e.IssueId, e.OccurredAt,
            new AssigneeChange(e.PreviousAssigneeId, e.AssigneeId), ct);
}

internal sealed class IssueStatusChangedWebhookHandler(WebhookOutbox outbox) : IDomainEventHandler<IssueStatusChanged>
{
    public Task Handle(IssueStatusChanged e, CancellationToken ct) =>
        outbox.EnqueueIssueAsync(WebhookEventTypes.IssueStatusChanged, e.IssueId, e.OccurredAt, new StatusChange(e.From, e.To), ct);
}

internal sealed class IssueCommentedWebhookHandler(WebhookOutbox outbox) : IDomainEventHandler<IssueCommented>
{
    public Task Handle(IssueCommented e, CancellationToken ct) =>
        outbox.EnqueueIssueAsync(WebhookEventTypes.IssueCommented, e.IssueId, e.OccurredAt, new CommentChange(e.EventId), ct);
}

internal sealed class TaskCompletedWebhookHandler(WebhookOutbox outbox) : IDomainEventHandler<TaskCompleted>
{
    public Task Handle(TaskCompleted e, CancellationToken ct) =>
        outbox.EnqueueTaskAsync(WebhookEventTypes.TaskCompleted, e.TaskId, e.OccurredAt, ct);
}

internal sealed class TaskApprovedWebhookHandler(WebhookOutbox outbox) : IDomainEventHandler<TaskApproved>
{
    public Task Handle(TaskApproved e, CancellationToken ct) =>
        outbox.EnqueueTaskAsync(WebhookEventTypes.TaskApproved, e.TaskId, e.OccurredAt, ct);
}
