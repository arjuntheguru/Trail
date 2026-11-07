using Trail.Domain.Common;

namespace Trail.Domain.Integrations;

/// <summary>An HTTPS endpoint an organization wants called when selected events happen.</summary>
public sealed class WebhookSubscription : TenantEntity, ISoftDeletable
{
    public const int UrlMaxLength = 2000;
    public const int DescriptionMaxLength = 300;
    public const int MaxPerOrganization = 25;

    /// <summary>Consecutive failed attempts after which the subscription is switched off.</summary>
    public const int AutoDisableAfterFailures = 50;

    public required string Url { get; set; }
    public string? Description { get; set; }

    /// <summary>Event names from <see cref="WebhookEventTypes.All"/>.</summary>
    public HashSet<string> EventTypes { get; set; } = [];

    /// <summary>
    /// Signing key for the HMAC of each delivery. Kept in clear text because signing needs the key itself; it is
    /// shown to the owner once, never returned by reads, and kept out of the audit trail.
    /// </summary>
    [NotAudited]
    public required string Secret { get; set; }

    public bool IsActive { get; set; } = true;

    // Delivery bookkeeping, maintained by the delivery job with bulk updates.
    [NotAudited] public int FailureCount { get; set; }
    [NotAudited] public DateTime? DisabledAt { get; set; }
    [NotAudited] public DateTime? LastSuccessAt { get; set; }
    [NotAudited] public DateTime? LastFailureAt { get; set; }

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
