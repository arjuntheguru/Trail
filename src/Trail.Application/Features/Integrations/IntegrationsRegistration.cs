using Microsoft.Extensions.DependencyInjection;
using Trail.Application.Features.Tasks;

namespace Trail.Application.Features.Integrations;

public static class IntegrationsRegistration
{
    /// <summary>Services of the integrations slice that are not use-case handlers (those register themselves by name).</summary>
    public static IServiceCollection AddIntegrationFeatures(this IServiceCollection services)
    {
        services.AddScoped<WebhookOutbox>();
        services.AddScoped<RecordLogWriter>();
        return services;
    }
}
