using Trail.Domain.Common;

namespace Trail.Domain.Identity;

/// <summary>
/// One link in a rotating refresh-token chain. Only the SHA-256 hash of the token is stored. Using a token
/// replaces it with a new one in the same <see cref="FamilyId"/>; presenting an already-used token means it
/// was stolen, so the whole family is revoked.
/// </summary>
public sealed class RefreshToken : Entity
{
    public Guid UserId { get; set; }
    /// <summary>Organization the session acts in; null for platform-admin sessions outside any organization.</summary>
    public Guid? OrganizationId { get; set; }
    public Guid FamilyId { get; set; }

    public required string TokenHash { get; set; }

    /// <summary>The user's security stamp when issued; a newer stamp invalidates the token.</summary>
    public Guid SecurityStamp { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public string? CreatedByIp { get; set; }
    public string? UserAgent { get; set; }

    public bool IsUsable(DateTime now) => UsedAt is null && RevokedAt is null && ExpiresAt > now;
}
