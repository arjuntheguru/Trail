using Trail.Domain.Common;

namespace Trail.Domain.Identity;

public enum UserTokenPurpose
{
    EmailConfirmation,
    PasswordReset,
    Invitation,
}

/// <summary>Single-use emailed token (confirmation, reset, invitation). Only its SHA-256 hash is stored.</summary>
public sealed class UserToken : Entity
{
    public static readonly TimeSpan PasswordResetLifetime = TimeSpan.FromHours(1);
    public static readonly TimeSpan EmailConfirmationLifetime = TimeSpan.FromDays(3);
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    public Guid UserId { get; set; }
    public UserTokenPurpose Purpose { get; set; }
    public required string TokenHash { get; set; }

    /// <summary>For invitations: the membership being accepted.</summary>
    public Guid? MembershipId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }

    public bool IsUsable(DateTime now) => UsedAt is null && ExpiresAt > now;
}
