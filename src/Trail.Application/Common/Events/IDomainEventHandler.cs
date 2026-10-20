using Trail.Domain.Common;

namespace Trail.Application.Common.Events;

/// <summary>
/// Reacts to a domain event inside the save that raised it, in the same tenant. Handlers add rows to the unit
/// of work (notifications, outbox messages) and must not save or call external services themselves: anything
/// slow or fallible goes through an outbox row that a background job delivers after the commit.
/// </summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task Handle(TEvent domainEvent, CancellationToken ct);
}

/// <summary>Runs the registered handlers for the events tracked entities have raised.</summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken ct);
}
