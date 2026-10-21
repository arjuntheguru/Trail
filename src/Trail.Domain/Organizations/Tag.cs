using Trail.Domain.Common;

namespace Trail.Domain.Organizations;

/// <summary>Free-form label an organization uses to group tasks, e.g. "Kitchen" or "Front of house".</summary>
public sealed class Tag : TenantEntity
{
    public const int NameMaxLength = 64;

    public required string Name { get; set; }

    /// <summary>Hex colour such as "#1F8A70", for display only.</summary>
    public string? Color { get; set; }
}
