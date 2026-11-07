using Trail.Domain.Common;

namespace Trail.Domain.Organizations;

/// <summary>A grouping of sites (e.g. "London") that an area manager can be assigned to as a whole.</summary>
public sealed class Region : TenantEntity, ISoftDeletable
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    public required string Name { get; set; }
    public string? Description { get; set; }

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
