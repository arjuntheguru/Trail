using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Trail.Application.Features.Integrations;

namespace Trail.Api.Infrastructure;

/// <summary>
/// Configures the limiter from the validated <see cref="RateLimitOptions"/>. Limits are enforced per instance (in
/// memory), so with N replicas the effective limit is N times higher. The limiter runs before authentication, so
/// partitions are the client IP (behind a proxy: the forwarded IP) or, for sensors, the API key's public prefix.
/// </summary>
internal sealed class RateLimitingSetup(IOptions<RateLimitOptions> options) : IConfigureOptions<RateLimiterOptions>
{
    public void Configure(RateLimiterOptions limiter)
    {
        var limits = options.Value;
        limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        limiter.OnRejected = RejectAsync;
        limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(ClientIp(context), _ => PerMinute(limits.GlobalPermitsPerMinute)));
        limiter.AddPolicy(RateLimitPolicies.Auth, context =>
            RateLimitPartition.GetFixedWindowLimiter(ClientIp(context), _ => PerMinute(limits.AuthPermitsPerMinute)));
        // The key's database id is not known yet: the partition is the key's public prefix, and requests without a
        // well-formed key share the client IP.
        limiter.AddPolicy(RateLimitPolicies.Sensors, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                ApiKeyFormat.TryParse(ApiKeyAuth.ReadKey(context.Request), out var prefix) ? $"key:{prefix}" : $"ip:{ClientIp(context)}",
                _ => PerMinute(limits.SensorPermitsPerMinute)));
    }

    private static string ClientIp(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static FixedWindowRateLimiterOptions PerMinute(int permits) =>
        new() { PermitLimit = permits, Window = TimeSpan.FromMinutes(1) };

    internal static async ValueTask RejectAsync(OnRejectedContext context, CancellationToken ct)
    {
        var http = context.HttpContext;
        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            http.Response.Headers.RetryAfter =
                Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

        var problems = http.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = { Status = StatusCodes.Status429TooManyRequests, Title = "Too many requests." },
        });
    }
}

/// <summary>Builds the CORS policy from the validated <see cref="FrontendCorsOptions"/>.</summary>
internal sealed class CorsSetup(IOptions<FrontendCorsOptions> options) : IConfigureOptions<CorsOptions>
{
    public void Configure(CorsOptions cors) =>
        cors.AddPolicy(FrontendCorsOptions.PolicyName, policy => policy
            .WithOrigins(options.Value.AllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
}
