using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Paging;
using Trail.Application.Common.Results;
using Trail.Domain.Integrations;

namespace Trail.Application.Features.Integrations;

public sealed class ListWebhookDeliveriesHandler(IAppDbContext db, ISiteAccess access)
{
    public async Task<Result<PagedResult<WebhookDeliveryResponse>>> Handle(Guid webhookId, PageRequest page, CancellationToken ct)
    {
        if (await IntegrationAccess.RequireAllSitesAsync(access, ct) is { } denied) return denied;
        if (!await db.WebhookSubscriptions.AnyAsync(w => w.Id == webhookId, ct)) return WebhookRules.NotFound;

        return await db.WebhookDeliveries.AsNoTracking()
            .Where(d => d.SubscriptionId == webhookId)
            .OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id)
            .Select(IntegrationMapping.DeliveryProjection)
            .ToPagedResultAsync(page, ct);
    }
}

/// <summary>Queues a signed "ping" so an owner can check their endpoint and secret handling.</summary>
public sealed class TestWebhookHandler(IAppDbContext db, ISiteAccess access, ITenantContext tenant, TimeProvider clock)
{
    public async Task<Result<WebhookDeliveryResponse>> Handle(Guid id, CancellationToken ct)
    {
        if (await IntegrationAccess.RequireAllSitesAsync(access, ct) is { } denied) return denied;
        var webhook = await db.WebhookSubscriptions.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, ct);
        if (webhook is null) return WebhookRules.NotFound;
        if (!webhook.IsActive) return Error.Conflict("webhook.inactive", "The webhook is switched off.");

        var now = clock.GetUtcNow().UtcDateTime;
        var eventId = Guid.CreateVersion7();
        var delivery = new WebhookDelivery
        {
            SubscriptionId = webhook.Id,
            EventId = eventId,
            EventType = WebhookEventTypes.Ping,
            Payload = WebhookPayloads.Build(eventId, WebhookEventTypes.Ping, now, tenant.OrganizationId!.Value,
                new PingWebhookData("Webhook test from Trail.")),
            NextAttemptAt = now,
        };
        db.WebhookDeliveries.Add(delivery);
        await db.SaveChangesAsync(ct);
        return IntegrationMapping.DeliveryProjection.Compile()(delivery);
    }
}

/// <summary>Sends a finished (succeeded or failed) delivery again with a fresh set of attempts.</summary>
public sealed class RedeliverWebhookHandler(IAppDbContext db, ISiteAccess access, TimeProvider clock)
{
    public async Task<Result<WebhookDeliveryResponse>> Handle(Guid deliveryId, CancellationToken ct)
    {
        if (await IntegrationAccess.RequireAllSitesAsync(access, ct) is { } denied) return denied;
        var delivery = await db.WebhookDeliveries.FirstOrDefaultAsync(d => d.Id == deliveryId, ct);
        if (delivery is null) return Error.NotFound("webhook.delivery_not_found", "Delivery not found.");

        if (delivery.Status is WebhookDeliveryStatus.Pending or WebhookDeliveryStatus.Sending)
            return Error.Conflict("webhook.delivery_in_progress", "The delivery has not finished yet.");

        var webhook = await db.WebhookSubscriptions.AsNoTracking().FirstOrDefaultAsync(w => w.Id == delivery.SubscriptionId, ct);
        if (webhook is null) return Error.NotFound("webhook.delivery_not_found", "Delivery not found.");
        if (!webhook.IsActive) return Error.Conflict("webhook.inactive", "The webhook is switched off.");

        delivery.Status = WebhookDeliveryStatus.Pending;
        delivery.Attempts = 0;
        delivery.NextAttemptAt = clock.GetUtcNow().UtcDateTime;
        delivery.ClaimToken = null;
        delivery.ClaimedAt = null;
        delivery.LastError = null;
        delivery.LastStatusCode = null;
        delivery.DeliveredAt = null;
        await db.SaveChangesAsync(ct);
        return IntegrationMapping.DeliveryProjection.Compile()(delivery);
    }
}
