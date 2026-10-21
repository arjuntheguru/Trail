using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Trail.Application.Features.Auth;
using Trail.Application.Features.Members;
using Trail.Application.Features.Organizations;

namespace Trail.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        // Every concrete "*Handler" class is a use case, resolved by type from endpoints.
        foreach (var handler in assembly.DefinedTypes.Where(t =>
                     t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                     && t.Name.EndsWith("Handler", StringComparison.Ordinal)))
        {
            services.AddScoped(handler.AsType());
        }

        services.AddScoped<OrganizationProvisioner>();
        services.AddScoped<SessionIssuer>();
        services.AddScoped<EmailConfirmationSender>();
        services.AddScoped<InvitationSender>();

        return services;
    }
}
