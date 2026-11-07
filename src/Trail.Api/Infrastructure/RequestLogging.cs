using Serilog;
using Serilog.Events;
using Trail.Application.Common.Security;

namespace Trail.Api.Infrastructure;

internal static class RequestLogging
{
    /// <summary>Error for 5xx and exceptions, Debug for probes so they do not flood the log, Information otherwise.</summary>
    public static LogEventLevel GetLevel(HttpContext context, double elapsedMs, Exception? exception)
    {
        if (exception is not null || context.Response.StatusCode >= 500) return LogEventLevel.Error;
        return context.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Debug : LogEventLevel.Information;
    }

    /// <summary>Adds who and from where. Runs after the pipeline, so the authenticated principal is available.</summary>
    public static void Enrich(IDiagnosticContext diagnostics, HttpContext context)
    {
        diagnostics.Set("ClientIp", context.Connection.RemoteIpAddress?.ToString());
        if (context.User.FindFirst(TrailClaimTypes.UserId)?.Value is { } user) diagnostics.Set("UserId", user);
        if (context.User.FindFirst(TrailClaimTypes.OrganizationId)?.Value is { } org) diagnostics.Set("OrganizationId", org);
    }
}
