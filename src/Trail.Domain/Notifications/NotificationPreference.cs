using Trail.Domain.Common;

namespace Trail.Domain.Notifications;

/// <summary>
/// How one membership wants to be told. A membership without a row gets the defaults, so rows are only created
/// when someone changes a setting or the digest job records that it ran.
/// </summary>
[NotAudited]
public sealed class NotificationPreference : TenantEntity
{
    public const int DefaultDigestHour = 7;

    /// <summary>Kinds emailed by default: the ones that need someone to act.</summary>
    public static readonly IReadOnlyList<NotificationKind> DefaultEmailKinds =
    [
        NotificationKind.IssueAssigned,
        NotificationKind.IssueEscalated,
        NotificationKind.IssueOverdue,
        NotificationKind.TaskAwaitingApproval,
    ];

    public Guid MembershipId { get; set; }

    public List<NotificationKind> EmailKinds { get; set; } = [.. DefaultEmailKinds];

    public bool DigestEnabled { get; set; } = true;

    /// <summary>Local hour (0-23) from which the daily digest may be sent.</summary>
    public int DigestHour { get; set; } = DefaultDigestHour;

    /// <summary>The local date of the last digest run for this membership, set before sending so a crash skips rather than repeats.</summary>
    public DateOnly? LastDigestOn { get; set; }

    public bool WantsEmail(NotificationKind kind) => EmailKinds.Contains(kind);

    public static bool WantsEmail(NotificationPreference? preference, NotificationKind kind) =>
        preference?.WantsEmail(kind) ?? DefaultEmailKinds.Contains(kind);
}
