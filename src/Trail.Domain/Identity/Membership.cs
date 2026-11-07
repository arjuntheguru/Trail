using Trail.Domain.Common;

namespace Trail.Domain.Identity;

public enum MembershipStatus
{
    Invited,
    Active,
    Suspended,
}

/// <summary>A user's place in one organization: their role and which sites they can work with.</summary>
public sealed class Membership : TenantEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public MembershipStatus Status { get; set; } = MembershipStatus.Invited;

    /// <summary>
    /// True: the member can see every site in the organization. False: only the sites and regions explicitly
    /// assigned to this membership.
    /// </summary>
    public bool AllSites { get; set; } = true;

    public DateTime? JoinedAt { get; set; }

    public bool CanSignIn => Status == MembershipStatus.Active;
}
