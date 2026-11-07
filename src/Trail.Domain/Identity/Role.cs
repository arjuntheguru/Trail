using Trail.Domain.Common;

namespace Trail.Domain.Identity;

/// <summary>A named set of permissions inside one organization. System roles are created with the organization.</summary>
public sealed class Role : TenantEntity
{
    public const int NameMaxLength = 64;
    public const int DescriptionMaxLength = 500;

    public required string Name { get; set; }
    public string? Description { get; set; }

    /// <summary>Built-in roles cannot be renamed or deleted; the Owner role's permissions cannot be changed.</summary>
    public bool IsSystem { get; set; }

    public List<string> Permissions { get; set; } = [];
}
