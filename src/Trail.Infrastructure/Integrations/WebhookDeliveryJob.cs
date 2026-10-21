using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trail.Application.Common.Abstractions;
using Trail.Application.Features.Integrations;
using Trail.Domain.Integrations;
using Trail.Infrastructure.Persistence;

namespace Trail.Infrastructure.Integrations;

/// <summary>
/// Sends the webhook outbox. Every tick it finds organizations with due deliveries and, per organization (own scope
/// and tenant), claims a batch, posts it and records the outcome. Safe on several instances at once: a delivery is
/// claimed with one conditional bulk update (Pending to Sending with a claim token), so only one instance gets it;
/// deliveries left in Sending by a crashed instance are put back after a few minutes.
/// </summary>
internal sealed class WebhookDeliveryJob(
    IServiceScopeFactory scopes, IOptions<WebhookOptions> options, TimeProvider clock, ILogger<WebhookDeliveryJob> logger)
    : BackgroundService
{
    private const int BatchSize = 50;
    private const int MaxBatchesPerOrganization = 10;
    private const int Concurrency = 8;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.IntervalSeconds), clock);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Webhook delivery run failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var stuckBefore = now - WebhookRetryPolicy.StuckAfter;

        List<Guid> organizationIds;
        await using (var scope = scopes.CreateAsyncScope())
        {
            // Background job with no tenant of its own: it only reads the ids of organizations that have work, then
            // handles each in a scope bound to that organization. Platform-level, so the tenant filter is ignored.
            organizationIds = await scope.ServiceProvider.GetRequiredService<TrailDbContext>().WebhookDeliveries
                .IgnoreQueryFilters([QueryFilters.Tenant])
                .AsNoTracking()
                .Where(d => (d.Status == WebhookDeliveryStatus.Pending && d.NextAttemptAt <= now)
                            || (d.Status == WebhookDeliveryStatus.Sending && d.ClaimedAt < stuckBefore))
                .Select(d => d.OrganizationId)
                .Distinct()
                .ToListAsync(ct);
        }

        foreach (var organizationId in organizationIds)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<ITenantContext>().UseOrganization(organizationId);
                await ProcessOrganizationAsync(scope.ServiceProvider, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One organization's problem must not hold up the others' deliveries.
                logger.LogError(ex, "Webhook delivery failed for organization {OrganizationId}.", organizationId);
            }
        }
    }

    private async Task ProcessOrganizationAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<TrailDbContext>();
        var sender = services.GetRequiredService<WebhookSender>();

        await RecoverStuckAsync(db, ct);

        for (var batch = 0; batch < MaxBatchesPerOrganization; batch++)
        {
            var claimed = await ClaimAsync(db, ct);
            if (claimed.Count == 0) return;

            var subscriptionIds = claimed.Select(d => d.SubscriptionId).Distinct().ToList();
            var subscriptions = await db.WebhookSubscriptions
                .IgnoreQueryFilters([QueryFilters.SoftDelete]) // a deleted subscription must fail its queued deliveries, not hide them
                .AsNoTracking()
                .Where(s => subscriptionIds.Contains(s.Id))
                .Select(s => new SubscriptionInfo(s.Id, s.Url, s.Secret, s.IsActive && s.DeletedAt == null))
                .ToDictionaryAsync(s => s.Id, ct);

            var outcomes = new Dictionary<Guid, WebhookSendResult>();
            var toSend = new List<(WebhookDelivery Delivery, SubscriptionInfo Subscription)>();
            foreach (var delivery in claimed)
            {
                if (subscriptions.TryGetValue(delivery.SubscriptionId, out var subscription) && subscription.IsActive)
                    toSend.Add((delivery, subscription));
                else
                    outcomes[delivery.Id] = new WebhookSendResult(false, null, "The webhook is switched off or deleted.");
            }

            var sent = new ConcurrentDictionary<Guid, WebhookSendResult>();
            await Parallel.ForEachAsync(toSend, new ParallelOptions { MaxDegreeOfParallelism = Concurrency, CancellationToken = ct },
                async (item, token) => sent[item.Delivery.Id] = await sender.SendAsync(
                    item.Subscription.Url, item.Delivery.EventType, item.Delivery.Id, item.Subscription.Secret, item.Delivery.Payload, token));
            foreach (var (id, result) in sent) outcomes[id] = result;

            foreach (var delivery in claimed)
            {
                var live = subscriptions.TryGetValue(delivery.SubscriptionId, out var info) && info.IsActive;
                await RecordOutcomeAsync(db, delivery, outcomes[delivery.Id], live, ct);
            }
        }
    }

    private async Task RecoverStuckAsync(TrailDbContext db, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var stuckBefore = now - WebhookRetryPolicy.StuckAfter;

        // A crash mid-send counts as an attempt (it was incremented at claim), so a poison delivery cannot loop forever.
        await db.WebhookDeliveries
            .Where(d => d.Status == WebhookDeliveryStatus.Sending && d.ClaimedAt < stuckBefore && d.Attempts >= WebhookDelivery.MaxAttempts)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Status, WebhookDeliveryStatus.Failed)
                .SetProperty(d => d.ClaimToken, (Guid?)null)
                .SetProperty(d => d.LastError, "The delivery was interrupted too many times."), ct);
        await db.WebhookDeliveries
            .Where(d => d.Status == WebhookDeliveryStatus.Sending && d.ClaimedAt < stuckBefore)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Status, WebhookDeliveryStatus.Pending)
                .SetProperty(d => d.ClaimToken, (Guid?)null)
                .SetProperty(d => d.NextAttemptAt, now), ct);
    }

    private async Task<List<WebhookDelivery>> ClaimAsync(TrailDbContext db, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var candidates = await db.WebhookDeliveries
            .Where(d => d.Status == WebhookDeliveryStatus.Pending && d.NextAttemptAt <= now)
            .OrderBy(d => d.NextAttemptAt).ThenBy(d => d.Id)
            .Select(d => d.Id)
            .Take(BatchSize)
            .ToListAsync(ct);
        if (candidates.Count == 0) return [];

        // The Status condition is what makes this atomic: when two instances race for a row, only one update matches.
        var token = Guid.NewGuid();
        await db.WebhookDeliveries
            .Where(d => candidates.Contains(d.Id) && d.Status == WebhookDeliveryStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Status, WebhookDeliveryStatus.Sending)
                .SetProperty(d => d.ClaimToken, token)
                .SetProperty(d => d.ClaimedAt, now)
                .SetProperty(d => d.Attempts, d => d.Attempts + 1), ct);

        return await db.WebhookDeliveries.AsNoTracking()
            .Where(d => d.ClaimToken == token && d.Status == WebhookDeliveryStatus.Sending)
            .ToListAsync(ct);
    }

    private async Task RecordOutcomeAsync(
        TrailDbContext db, WebhookDelivery claimed, WebhookSendResult result, bool subscriptionLive, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var token = claimed.ClaimToken;
        var error = result.Error is { Length: > WebhookDelivery.ErrorMaxLength } text
            ? text[..WebhookDelivery.ErrorMaxLength]
            : result.Error;
        var code = result.StatusCode;

        var deliveries = db.WebhookDeliveries.Where(d => d.Id == claimed.Id && d.ClaimToken == token);
        var subscriptions = db.WebhookSubscriptions.Where(s => s.Id == claimed.SubscriptionId);

        if (result.Succeeded)
        {
            var updated = await deliveries.ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Status, WebhookDeliveryStatus.Succeeded)
                .SetProperty(d => d.DeliveredAt, now)
                .SetProperty(d => d.LastStatusCode, code)
                .SetProperty(d => d.LastError, (string?)null)
                .SetProperty(d => d.ClaimToken, (Guid?)null), ct);
            // No row updated means another run owns the delivery now (it was recovered as stuck); it records the outcome.
            if (updated == 0) return;
            await subscriptions.ExecuteUpdateAsync(s => s
                .SetProperty(w => w.FailureCount, 0)
                .SetProperty(w => w.LastSuccessAt, now), ct);
            return;
        }

        // A deleted or switched-off subscription is final: retrying cannot help.
        var (status, next) = subscriptionLive
            ? WebhookRetryPolicy.OnFailure(claimed.Attempts, now)
            : (WebhookDeliveryStatus.Failed, now);
        var recorded = await deliveries.ExecuteUpdateAsync(s => s
            .SetProperty(d => d.Status, status)
            .SetProperty(d => d.NextAttemptAt, next)
            .SetProperty(d => d.LastStatusCode, code)
            .SetProperty(d => d.LastError, error)
            .SetProperty(d => d.ClaimToken, (Guid?)null), ct);

        if (recorded == 0 || !subscriptionLive) return;

        await subscriptions.ExecuteUpdateAsync(s => s
            .SetProperty(w => w.FailureCount, w => w.FailureCount + 1)
            .SetProperty(w => w.LastFailureAt, now), ct);
        var disabled = await subscriptions
            .Where(w => w.IsActive && w.FailureCount >= WebhookSubscription.AutoDisableAfterFailures)
            .ExecuteUpdateAsync(s => s
                .SetProperty(w => w.IsActive, false)
                .SetProperty(w => w.DisabledAt, now), ct);
        if (disabled > 0)
            logger.LogWarning("Webhook {SubscriptionId} was switched off after {Failures} consecutive failures.",
                claimed.SubscriptionId, WebhookSubscription.AutoDisableAfterFailures);
    }

    private sealed record SubscriptionInfo(Guid Id, string Url, string Secret, bool IsActive);
}
