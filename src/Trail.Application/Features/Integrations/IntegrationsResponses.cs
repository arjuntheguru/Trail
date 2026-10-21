using System.Linq.Expressions;
using Trail.Domain.Integrations;

namespace Trail.Application.Features.Integrations;

public sealed record WebhookResponse(
    Guid Id, string Url, string? Description, IReadOnlyCollection<string> EventTypes, bool IsActive, int FailureCount,
    DateTime? DisabledAt, DateTime? LastSuccessAt, DateTime? LastFailureAt, DateTime CreatedAt);

/// <summary>Returned when a webhook is created or its secret rotated: the only time the secret is shown.</summary>
public sealed record WebhookWithSecretResponse(WebhookResponse Webhook, string Secret);

public sealed record WebhookDeliveryResponse(
    Guid Id, Guid EventId, string EventType, WebhookDeliveryStatus Status, int Attempts, DateTime NextAttemptAt,
    int? LastStatusCode, string? LastError, DateTime? DeliveredAt, DateTime CreatedAt);

public sealed record ApiKeyResponse(
    Guid Id, string Name, string Prefix, IReadOnlyCollection<string> Scopes, IReadOnlyCollection<Guid> SiteIds,
    DateTime? ExpiresAt, DateTime? LastUsedAt, DateTime? RevokedAt, DateTime CreatedAt);

/// <summary>Returned once when a key is created; <see cref="Key"/> cannot be retrieved again.</summary>
public sealed record ApiKeyCreatedResponse(ApiKeyResponse ApiKey, string Key);

public sealed record SensorDeviceResponse(
    Guid Id, string ExternalId, string Name, Guid SiteId, Guid TemplateId, string FieldKey,
    DateTime? LastReadingAt, decimal? LastValue);

public sealed record SensorReadingResult(int Index, string DeviceId, string Result, string? Detail = null);

public sealed record SensorReadingsResponse(
    int Accepted, int Skipped, int Rejected, IReadOnlyList<SensorReadingResult> Results);

/// <summary>Projections for reads (they never select the webhook secret or key hash) and the same shapes for tracked entities.</summary>
internal static class IntegrationMapping
{
    public static readonly Expression<Func<WebhookSubscription, WebhookResponse>> WebhookProjection = s => new WebhookResponse(
        s.Id, s.Url, s.Description, s.EventTypes, s.IsActive, s.FailureCount,
        s.DisabledAt, s.LastSuccessAt, s.LastFailureAt, s.CreatedAt);

    public static readonly Expression<Func<ApiKey, ApiKeyResponse>> ApiKeyProjection = k => new ApiKeyResponse(
        k.Id, k.Name, k.Prefix, k.Scopes, k.SiteIds ?? new List<Guid>(),
        k.ExpiresAt, k.LastUsedAt, k.RevokedAt, k.CreatedAt);

    public static readonly Expression<Func<SensorDevice, SensorDeviceResponse>> DeviceProjection = d => new SensorDeviceResponse(
        d.Id, d.ExternalId, d.Name, d.SiteId, d.TemplateId, d.FieldKey, d.LastReadingAt, d.LastValue);

    public static readonly Expression<Func<WebhookDelivery, WebhookDeliveryResponse>> DeliveryProjection = d => new WebhookDeliveryResponse(
        d.Id, d.EventId, d.EventType, d.Status, d.Attempts, d.NextAttemptAt, d.LastStatusCode, d.LastError, d.DeliveredAt, d.CreatedAt);

    private static readonly Func<WebhookSubscription, WebhookResponse> Webhook = WebhookProjection.Compile();
    private static readonly Func<ApiKey, ApiKeyResponse> Key = ApiKeyProjection.Compile();
    private static readonly Func<SensorDevice, SensorDeviceResponse> Device = DeviceProjection.Compile();

    public static WebhookResponse ToResponse(this WebhookSubscription s) => Webhook(s);
    public static ApiKeyResponse ToResponse(this ApiKey k) => Key(k);
    public static SensorDeviceResponse ToResponse(this SensorDevice d) => Device(d);
}
