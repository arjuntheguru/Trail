using Trail.Domain.Common;

namespace Trail.Domain.Notifications;

public enum NotificationKind
{
    IssueAssigned,
    IssueRaised,
    IssueStatusChanged,
    IssueCommented,
    IssueOverdue,
    IssueEscalated,
    TaskAwaitingApproval,
}

public enum NotificationSubjectType
{
    Issue,
    Task,
}

public enum NotificationEmailStatus
{
    /// <summary>Not decided yet; the default for a row that was never routed to email.</summary>
    None,

    /// <summary>Waiting for the email job (or for a retry after a failed attempt).</summary>
    Pending,

    /// <summary>Claimed by one email job run, which is sending it right now.</summary>
    Sending,

    Sent,

    /// <summary>Gave up after <see cref="Notification.MaxEmailAttempts"/> attempts.</summary>
    Failed,

    /// <summary>Not emailed: the recipient's preferences exclude it, or they cannot receive email.</summary>
    Skipped,
}

/// <summary>
/// An in-app notification for one membership, optionally also emailed. Created by domain event handlers inside the
/// save of the change that caused it; read and cleared by its recipient. Hard-deleted, and kept out of the audit
/// trail because it is derived data.
/// </summary>
[NotAudited]
public sealed class Notification : TenantEntity
{
    public const int TitleMaxLength = 200;
    public const int BodyMaxLength = 1000;
    public const int MaxEmailAttempts = 5;

    public Guid MembershipId { get; set; }
    public NotificationKind Kind { get; set; }
    public required string Title { get; set; }
    public required string Body { get; set; }

    public NotificationSubjectType SubjectType { get; set; }
    public Guid SubjectId { get; set; }
    public Guid? SiteId { get; set; }

    public DateTime? ReadAt { get; set; }

    public NotificationEmailStatus EmailStatus { get; set; }
    public int EmailAttempts { get; set; }
    public DateTime? EmailSentAt { get; set; }

    /// <summary>Identifies the email job run that claimed this row while its status is <see cref="NotificationEmailStatus.Sending"/>.</summary>
    public Guid? EmailClaimToken { get; set; }
}
