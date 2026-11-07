using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trail.Application.Common.Abstractions;
using Trail.Application.Features.Tasks;
using Trail.Infrastructure.Persistence;

namespace Trail.Infrastructure.Jobs;

public sealed class TaskGenerationOptions
{
    public const string SectionName = "TaskGeneration";

    public bool Enabled { get; set; } = true;

    /// <summary>How often lists are topped up and ended days are closed. Sites change day at different times.</summary>
    [Range(1, 1440)]
    public int IntervalMinutes { get; set; } = 10;
}

/// <summary>
/// Runs <see cref="TaskGenerator"/> for every active organization on a timer, each in its own scope and tenant.
/// Safe to run on several instances at once: generation is idempotent and missed-marking is a conditional update.
/// </summary>
internal sealed class TaskGenerationJob(
    IServiceScopeFactory scopes, IDistributedLock locks, IOptions<TaskGenerationOptions> options, TimeProvider clock, ILogger<TaskGenerationJob> logger)
    : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        options.Value.Enabled
            ? ExclusiveTick.LoopAsync(TimeSpan.FromMinutes(options.Value.IntervalMinutes), clock, logger, JobName, TickAsync, stoppingToken)
            : Task.CompletedTask;

    internal const string JobName = "task-generation";

    /// <summary>One scheduled tick: skipped when another replica is already running it.</summary>
    internal Task<bool> TickAsync(CancellationToken ct) =>
        ExclusiveTick.RunAsync(locks, JobName, logger, RunOnceAsync, ct);

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
                var generator = scope.ServiceProvider.GetRequiredService<TaskGenerator>();
                var missed = await generator.MarkMissedAsync(ct);
                var created = await generator.GenerateAsync(ct);
                if (created + missed > 0)
                    logger.LogInformation("Organization {OrganizationId}: {Created} tasks created, {Missed} marked missed.",
                        organizationId, created, missed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One organization's bad data must not stop the others' lists.
                logger.LogError(ex, "Task generation failed for organization {OrganizationId}.", organizationId);
            }
        }
    }
}
