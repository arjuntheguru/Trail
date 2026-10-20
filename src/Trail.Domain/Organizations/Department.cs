using Trail.Domain.Common;

namespace Trail.Domain.Organizations;

/// <summary>An organization-wide function such as Kitchen, Front of House or Bar.</summary>
public sealed class Department : TenantEntity, ISoftDeletable
{
    public const int NameMaxLength = 100;

    public required string Name { get; set; }

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
