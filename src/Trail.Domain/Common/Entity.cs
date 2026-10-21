namespace Trail.Domain.Common;

/// <summary>Base type for everything persisted. Ids are time-ordered GUIDs (v7) so they index well.</summary>
public abstract class Entity
{
    private List<IDomainEvent>? _domainEvents;

    public Guid Id { get; protected init; } = Guid.CreateVersion7();

    /// <summary>Events raised since the last save. Not persisted.</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents ?? [];

    protected void Raise(IDomainEvent domainEvent) => (_domainEvents ??= []).Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents?.Clear();
}

/// <summary>Entity with audit columns and an optimistic-concurrency stamp, all maintained by the persistence layer.</summary>
public abstract class AuditableEntity : Entity, IAuditable
{
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedById { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}

/// <summary>
/// Entity owned by one organization (tenant). The persistence layer stamps <see cref="OrganizationId"/>
/// on insert, rejects writes across tenants and filters every query to the caller's organization.
/// </summary>
public abstract class TenantEntity : AuditableEntity, ITenantOwned
{
    public Guid OrganizationId { get; set; }
}
