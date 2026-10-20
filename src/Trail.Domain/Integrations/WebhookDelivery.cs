using Trail.Domain.Common;

namespace Trail.Domain.Integrations;

public enum WebhookDeliveryStatus
{
    /// <summary>Waiting for its first attempt or the next retry.</summary>
    Pending,

    /// <summary>Claimed by a delivery job instance that is sending it now.</summary>
    Sending,

    Succeeded,

    /// <summary>Out of attempts (or the subscription is gone). Can be redelivered by hand.</summary>
    Failed,
}

/// <summary>
/// The outbox: one row per event per subscription, written in the same save as the change that caused it and
/// delivered afterwards by the delivery job. The payload is stored as the exact text that is signed and sent.
/// </summary>
[NotAudited]
public sealed class WebhookDelivery : TenantEntity
{
    public const int EventTypeMaxLength = 64;
    public const int ErrorMaxLength = 1000;
    public const int MaxAttempts = 8;

    public Guid SubscriptionId { get; set; }

    /// <summary>Identifies the occurrence; the same id is in the payload and unique per subscription.</summary>
    public Guid EventId { get; set; }

    public required string EventType { get; set; }
    public required string Payload { get; set; }

    public WebhookDeliveryStatus Status { get; set; } = WebhookDeliveryStatus.Pending;
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; }

    /// <summary>Set while <see cref="Status"/> is Sending; lets several job instances share the table without double sends.</summary>
    public Guid? ClaimToken { get; set; }
    public DateTime? ClaimedAt { get; set; }

    public int? LastStatusCode { get; set; }
    public string? LastError { get; set; }
    public DateTime? DeliveredAt { get; set; }
}
