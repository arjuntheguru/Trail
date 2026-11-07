using Trail.Domain.Common;

namespace Trail.Domain.Identity;

/// <summary>
/// A person who can sign in. Users are global: the same login can be a member of several organizations
/// through <see cref="Membership"/>. Users are deactivated, never deleted, so history keeps its authors.
/// </summary>
public sealed class User : AuditableEntity
{
    public const int EmailMaxLength = 254;
    public const int NameMaxLength = 100;
    public const int MaxFailedAccessAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public required string Email { get; set; }

    /// <summary>Upper-invariant email used for lookups and the unique index.</summary>
    public required string NormalizedEmail { get; set; }

    public required string FirstName { get; set; }
    public required string LastName { get; set; }

    /// <summary>ASP.NET Core Identity v3 hash (PBKDF2-HMAC-SHA512). Null until an invited user sets a password.</summary>
    [NotAudited]
    public string? PasswordHash { get; set; }

    public DateTime? EmailConfirmedAt { get; set; }

    /// <summary>Changes whenever credentials change; refresh tokens issued under an older stamp stop working.</summary>
    [NotAudited]
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();

    [NotAudited]
    public int AccessFailedCount { get; set; }
    [NotAudited]
    public DateTime? LockoutEndsAt { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsPlatformAdmin { get; set; }

    [NotAudited]
    public DateTime? LastLoginAt { get; set; }

    /// <summary>Organization selected at the last sign-in, used as the default next time.</summary>
    [NotAudited]
    public Guid? LastOrganizationId { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    public static string Normalize(string email) => email.Trim().ToUpperInvariant();

    public bool IsLockedOut(DateTime now) => LockoutEndsAt is { } ends && ends > now;

    public void RecordFailedSignIn(DateTime now)
    {
        AccessFailedCount++;
        if (AccessFailedCount >= MaxFailedAccessAttempts)
        {
            LockoutEndsAt = now + LockoutDuration;
            AccessFailedCount = 0;
        }
    }

    public void RecordSuccessfulSignIn(DateTime now, Guid? organizationId)
    {
        AccessFailedCount = 0;
        LockoutEndsAt = null;
        LastLoginAt = now;
        LastOrganizationId = organizationId;
    }

    public void SetPasswordHash(string hash)
    {
        PasswordHash = hash;
        SecurityStamp = Guid.NewGuid();
        AccessFailedCount = 0;
        LockoutEndsAt = null;
    }
}
