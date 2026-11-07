namespace Trail.Domain.Common;

/// <summary>
/// Something that happened to an entity that other parts of the system react to (notifications, webhooks).
/// Raised by domain methods and handled in the same save, before it commits.
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredAt { get; }
}
