using System.Text.Json;
using Trail.Domain.Common;

namespace Trail.Domain.Auditing;

public enum AuditAction
{
    Created,
    Updated,
    Deleted,
}

/// <summary>A property's value before and after a change, as JSON. Old is null on create, New on delete.</summary>
public sealed record AuditChange(JsonElement? Old, JsonElement? New);

/// <summary>
/// An append-only record of one change to one row, written by the persistence layer in the same transaction
/// as the change itself, so the trail cannot miss a committed write or include a rolled-back one.
/// </summary>
[NotAudited]
public sealed class AuditEntry : Entity
{
    public const int EntityTypeMaxLength = 100;

    /// <summary>Null for platform-level rows such as users, which only platform staff can see.</summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>The site the row belongs to, when it has one, so site-scoped members see only their sites.</summary>
    public Guid? SiteId { get; set; }

    public required string EntityType { get; set; }
    public Guid EntityId { get; set; }
    public AuditAction Action { get; set; }
    public Dictionary<string, AuditChange> Changes { get; set; } = [];

    public Guid? ActorId { get; set; }
    public DateTime OccurredAt { get; set; }
    public string? IpAddress { get; set; }

    /// <summary>W3C trace id of the request, to group the rows one action changed.</summary>
    public string? TraceId { get; set; }
}
