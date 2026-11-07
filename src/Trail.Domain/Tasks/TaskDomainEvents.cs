using Trail.Domain.Common;

namespace Trail.Domain.Tasks;

// Events carry ids and what changed. Handlers read the rest from the tracked task.

public sealed record TaskCompleted(Guid TaskId, Guid SiteId, Guid? ActorId, DateTime OccurredAt) : IDomainEvent;

public sealed record TaskApproved(Guid TaskId, Guid SiteId, Guid? ActorId, DateTime OccurredAt) : IDomainEvent;
