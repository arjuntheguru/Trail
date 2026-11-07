using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Trail.Infrastructure.Telemetry;

namespace Trail.Api.Infrastructure;

internal static class Observability
{
    public const string ServiceName = "trail-api";
    public const string OtlpEndpointKey = "OpenTelemetry:OtlpEndpoint";
    public const string StandardOtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    /// <summary>
    /// Traces and metrics for ASP.NET Core, outgoing HTTP, the runtime and the "Trail" source/meter. Telemetry is
    /// exported over OTLP (gRPC) only when OTEL_EXPORTER_OTLP_ENDPOINT or OpenTelemetry:OtlpEndpoint is set.
    /// </summary>
    public static IServiceCollection AddTrailObservability(this IServiceCollection services, IConfiguration configuration)
    {
        var otel = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName, serviceVersion: Version()))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(o => o.Filter = IsTraced)
                .AddHttpClientInstrumentation()
                .AddSource(TrailTelemetry.Name))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(TrailTelemetry.Name));

        if (OtlpEndpoint(configuration) is { } endpoint)
            otel.UseOtlpExporter(OtlpExportProtocol.Grpc, endpoint);

        return services;
    }

    /// <summary>Health probes run every few seconds and would drown real traces.</summary>
    internal static bool IsTraced(HttpContext context) => !context.Request.Path.StartsWithSegments("/health");

    internal static Uri? OtlpEndpoint(IConfiguration configuration)
    {
        var value = configuration[StandardOtlpEndpointKey];
        if (string.IsNullOrWhiteSpace(value)) value = configuration[OtlpEndpointKey];
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
    }

    private static string Version()
    {
        var info = typeof(Observability).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion;
        return info?.Split('+')[0] ?? "0.0.0";
    }
}
