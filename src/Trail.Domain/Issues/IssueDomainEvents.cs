using Trail.Domain.Common;

namespace Trail.Domain.Issues;

// Events carry ids and what changed. Handlers read the rest (number, title, assignee) from the tracked issue,
// which is complete by the time the save dispatches them.

public sealed record IssueRaised(Guid IssueId, Guid SiteId, Guid? ActorId, DateTime OccurredAt) : IDomainEvent;

/// <summary>Assignee ids are membership ids; null means unassigned.</summary>
public sealed record IssueAssigned(
    Guid IssueId, Guid SiteId, Guid? PreviousAssigneeId, Guid? AssigneeId, Guid? ActorId, DateTime OccurredAt) : IDomainEvent;

public sealed record IssueStatusChanged(
    Guid IssueId, Guid SiteId, IssueStatus From, IssueStatus To, string? Note, Guid? ActorId, DateTime OccurredAt) : IDomainEvent;

public sealed record IssueCommented(Guid IssueId, Guid SiteId, Guid EventId, Guid? ActorId, DateTime OccurredAt) : IDomainEvent;
