using Microsoft.Extensions.Diagnostics.HealthChecks;
using Trail.Application.Common.Abstractions;

namespace Trail.Infrastructure.Files;

/// <summary>Implemented by storage backends so the readiness check can verify them cheaply.</summary>
internal interface IStorageProbe
{
    Task<bool> ProbeAsync(CancellationToken ct);
}

internal sealed class StorageHealthCheck(IFileStorage storage) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        if (storage is not IStorageProbe probe) return HealthCheckResult.Healthy("Storage is not probed.");
        bool ok;
        try { ok = await probe.ProbeAsync(ct); }
        catch (OperationCanceledException) { throw; }
        catch { ok = false; }
        // Descriptions stay generic: backend errors can include bucket names, endpoints or paths.
        return ok ? HealthCheckResult.Healthy("Storage is reachable.") : HealthCheckResult.Unhealthy("Storage is unavailable.");
    }
}
