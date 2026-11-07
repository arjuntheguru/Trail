using Trail.Domain.Common;

namespace Trail.Domain.Identity;

// Explicit access assignments for memberships that do not have Membership.AllSites. A membership can reach
// the union of its assigned sites and every site in its assigned regions.

/// <summary>Grants a membership access to one site.</summary>
public sealed class MembershipSite : TenantEntity
{
    public Guid MembershipId { get; set; }
    public Guid SiteId { get; set; }
}

/// <summary>Grants a membership access to every site in a region.</summary>
public sealed class MembershipRegion : TenantEntity
{
    public Guid MembershipId { get; set; }
    public Guid RegionId { get; set; }
}

/// <summary>Assigns a membership to a department.</summary>
public sealed class MembershipDepartment : TenantEntity
{
    public Guid MembershipId { get; set; }
    public Guid DepartmentId { get; set; }
}
