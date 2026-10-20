using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Domain.Integrations;

namespace Trail.Application.Features.Integrations;

/// <summary>
/// Turns one event into a Pending <see cref="WebhookDelivery"/> per active subscription that wants it. Only adds
/// rows to the unit of work: the save that carries the change commits them, and the delivery job sends them.
/// </summary>
public sealed class WebhookOutbox(IAppDbContext db, ITenantContext tenant, TimeProvider clock)
{
    public async Task EnqueueIssueAsync(string eventType, Guid issueId, DateTime occurredAt, object? change, CancellationToken ct)
    {
        // The issue is tracked by the save in progress, so this does not query.
        var issue = await db.Issues.FindAsync([issueId], ct);
        if (issue is null) return;
        await EnqueueAsync(eventType, occurredAt, WebhookPayloads.ForIssue(issue, change), ct);
    }

    public async Task EnqueueTaskAsync(string eventType, Guid taskId, DateTime occurredAt, CancellationToken ct)
    {
        var task = await db.SiteTasks.FindAsync([taskId], ct);
        if (task is null) return;
        await EnqueueAsync(eventType, occurredAt, WebhookPayloads.ForTask(task), ct);
    }

    public async Task EnqueueAsync(string eventType, DateTime occurredAt, object data, CancellationToken ct)
    {
        var subscriptions = (await db.WebhookSubscriptions.AsNoTracking()
                .Where(s => s.IsActive)
                .Select(s => new { s.Id, s.EventTypes })
                .ToListAsync(ct))
            .Where(s => s.EventTypes.Contains(eventType))
            .ToList();
        if (subscriptions.Count == 0) return;

        var organizationId = tenant.OrganizationId
            ?? throw new InvalidOperationException("Webhook events need an active organization.");

        // One payload, one event id, shared by every subscriber; (subscription, event id) is unique.
        var eventId = Guid.CreateVersion7();
        var payload = WebhookPayloads.Build(eventId, eventType, occurredAt, organizationId, data);
        var now = clock.GetUtcNow().UtcDateTime;

        foreach (var subscription in subscriptions)
        {
            db.WebhookDeliveries.Add(new WebhookDelivery
            {
                SubscriptionId = subscription.Id,
                EventId = eventId,
                EventType = eventType,
                Payload = payload,
                NextAttemptAt = now,
            });
        }
    }
}
