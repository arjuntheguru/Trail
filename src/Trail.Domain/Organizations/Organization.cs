using Trail.Domain.Common;

namespace Trail.Domain.Organizations;

/// <summary>A customer business. The tenant boundary: every <see cref="ITenantOwned"/> row belongs to exactly one.</summary>
public sealed class Organization : AuditableEntity, ISoftDeletable
{
    public const int NameMaxLength = 200;
    public const int SlugMaxLength = 64;

    public required string Name { get; set; }

    /// <summary>URL-safe unique handle, e.g. "dailo-foods".</summary>
    public required string Slug { get; set; }

    /// <summary>IANA time zone used when a site does not set its own.</summary>
    public string DefaultTimeZone { get; set; } = "UTC";

    public bool IsActive { get; set; } = true;

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
