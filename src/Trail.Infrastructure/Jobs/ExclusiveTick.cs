using Microsoft.Extensions.Logging;
using Trail.Application.Common.Abstractions;
using Trail.Infrastructure.Telemetry;

namespace Trail.Infrastructure.Jobs;

/// <summary>Runs one scheduled tick only on the replica that holds the job's distributed lock.</summary>
internal static class ExclusiveTick
{
    public static string LockName(string job) => $"job:{job}";

    /// <summary>Runs <paramref name="work"/> under the lock; returns false (and skips the tick) when another replica holds it.</summary>
    public static async Task<bool> RunAsync(
        IDistributedLock locks, string job, ILogger logger, Func<CancellationToken, Task> work, CancellationToken ct)
    {
        var handle = await locks.TryAcquireAsync(LockName(job), TimeSpan.Zero, ct);
        if (handle is null)
        {
            logger.LogDebug("{Job} tick skipped: another instance holds the lock.", job);
            return false;
        }

        await using (handle)
            await work(ct);
        return true;
    }

    /// <summary>
    /// The shared job loop: one tick per interval, a failing tick is logged and the next one waits for the timer
    /// (never a tight retry loop), and shutdown ends the loop quietly.
    /// </summary>
    public static async Task LoopAsync(
        TimeSpan interval, TimeProvider clock, ILogger logger, string job, Func<CancellationToken, Task> tick, CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(interval, clock);
        try
        {
            do
            {
                using var activity = TrailTelemetry.StartJob(job);
                TrailTelemetry.RecordJobRun(job);
                try
                {
                    await tick(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    TrailTelemetry.RecordJobFailure(job);
                    activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
                    logger.LogError(ex, "{Job} run failed.", job);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }
}
