using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

namespace Trail.Api.Infrastructure;

internal static class EdgeSetup
{
    /// <summary>Registers the validated edge options (CORS, rate limits, proxy trust) and the services built from them.</summary>
    public static IServiceCollection AddTrailEdge(this IServiceCollection services)
    {
        services.AddOptions<FrontendCorsOptions>().BindConfiguration(FrontendCorsOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<FrontendCorsOptions>, FrontendCorsOptionsValidator>();

        services.AddOptions<RateLimitOptions>().BindConfiguration(RateLimitOptions.SectionName)
            .ValidateDataAnnotations().ValidateOnStart();

        services.AddOptions<HttpOptions>().BindConfiguration(HttpOptions.SectionName)
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<HttpOptions>, HttpOptionsValidator>();

        services.AddCors();
        services.AddSingleton<IConfigureOptions<CorsOptions>, CorsSetup>();
        services.AddRateLimiter(_ => { });
        services.AddSingleton<IConfigureOptions<RateLimiterOptions>, RateLimitingSetup>();

        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<HttpOptions>>((forwarded, http) => http.Value.ApplyTo(forwarded));

        services.AddOptions<KestrelServerOptions>().Configure<IOptions<HttpOptions>>((kestrel, http) =>
        {
            kestrel.AddServerHeader = false;
            // Endpoints with their own [RequestSizeLimit] metadata (attachment upload) override this per request.
            kestrel.Limits.MaxRequestBodySize = http.Value.MaxRequestBodyBytes;
        });

        return services;
    }
}
