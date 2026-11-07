using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trail.Application.Common.Abstractions;
using Trail.Application.Features.Notifications;
using Trail.Infrastructure.Persistence;

namespace Trail.Infrastructure.Notifications;

public sealed class NotificationEmailOptions
{
    public const string SectionName = "NotificationEmail";

    public bool Enabled { get; set; } = true;

    [Range(1, 1440)]
    public int IntervalMinutes { get; set; } = 1;
}

public sealed class IssueEscalationOptions
{
    public const string SectionName = "IssueEscalation";

    public bool Enabled { get; set; } = true;

    [Range(1, 1440)]
    public int IntervalMinutes { get; set; } = 5;
}

public sealed class DailyDigestOptions
{
    public const string SectionName = "DailyDigest";

    public bool Enabled { get; set; } = true;

    /// <summary>How often the job checks whose digest hour has arrived. Digests are sent at most once a day each.</summary>
    [Range(1, 1440)]
    public int IntervalMinutes { get; set; } = 15;
}

/// <summary>
/// Runs a unit of work for every active organization on a timer, each in its own scope and tenant, like the task
/// generation job. One organization failing must not stop the others.
/// </summary>
internal abstract class PerOrganizationJob(IServiceScopeFactory scopes, TimeProvider clock, ILogger logger) : BackgroundService
{
    protected abstract string Name { get; }
    protected abstract bool Enabled { get; }
    protected abstract TimeSpan Interval { get; }

    protected abstract Task RunForOrganizationAsync(IServiceProvider services, Guid organizationId, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Enabled) return;

        using var timer = new PeriodicTimer(Interval, clock);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "{Job} run failed.", Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        List<Guid> organizationIds;
        await using (var scope = scopes.CreateAsyncScope())
        {
            organizationIds = await scope.ServiceProvider.GetRequiredService<TrailDbContext>().Organizations
                .AsNoTracking().Where(o => o.IsActive).Select(o => o.Id).ToListAsync(ct);
        }

        foreach (var organizationId in organizationIds)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<ITenantContext>().UseOrganization(organizationId);
                await RunForOrganizationAsync(scope.ServiceProvider, organizationId, ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another instance changed the same rows first and its work stands; the next tick starts afresh.
                logger.LogInformation("{Job} for organization {OrganizationId} lost a race with another instance.", Name, organizationId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "{Job} failed for organization {OrganizationId}.", Name, organizationId);
            }
        }
    }
}

/// <summary>Sends pending notification emails, retrying failures with backoff.</summary>
internal sealed class NotificationEmailJob(
    IServiceScopeFactory scopes, IOptions<NotificationEmailOptions> options, TimeProvider clock, ILogger<NotificationEmailJob> logger)
    : PerOrganizationJob(scopes, clock, logger)
{
    protected override string Name => "Notification email";
    protected override bool Enabled => options.Value.Enabled;
    protected override TimeSpan Interval => TimeSpan.FromMinutes(options.Value.IntervalMinutes);

    protected override async Task RunForOrganizationAsync(IServiceProvider services, Guid organizationId, CancellationToken ct) =>
        await services.GetRequiredService<NotificationEmailRunner>().SendPendingAsync(ct);
}

/// <summary>Announces overdue issues and escalates the ones that stay overdue.</summary>
internal sealed class IssueEscalationJob(
    IServiceScopeFactory scopes, IOptions<IssueEscalationOptions> options, TimeProvider clock, ILogger<IssueEscalationJob> logger)
    : PerOrganizationJob(scopes, clock, logger)
{
    protected override string Name => "Issue escalation";
    protected override bool Enabled => options.Value.Enabled;
    protected override TimeSpan Interval => TimeSpan.FromMinutes(options.Value.IntervalMinutes);

    protected override async Task RunForOrganizationAsync(IServiceProvider services, Guid organizationId, CancellationToken ct) =>
        await services.GetRequiredService<IssueEscalationRunner>().RunAsync(ct);
}

/// <summary>Sends each person's daily summary once their local digest hour has arrived.</summary>
internal sealed class DailyDigestJob(
    IServiceScopeFactory scopes, IOptions<DailyDigestOptions> options, TimeProvider clock, ILogger<DailyDigestJob> logger)
    : PerOrganizationJob(scopes, clock, logger)
{
    protected override string Name => "Daily digest";
    protected override bool Enabled => options.Value.Enabled;
    protected override TimeSpan Interval => TimeSpan.FromMinutes(options.Value.IntervalMinutes);

    protected override async Task RunForOrganizationAsync(IServiceProvider services, Guid organizationId, CancellationToken ct) =>
        await services.GetRequiredService<DailyDigestRunner>().RunAsync(ct);
}
