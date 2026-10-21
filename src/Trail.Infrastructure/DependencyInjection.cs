using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Trail.Application.Common.Abstractions;
using Trail.Infrastructure.Persistence;
using Trail.Infrastructure.Persistence.Interceptors;
using Trail.Infrastructure.Tenancy;

namespace Trail.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddOptions<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<AuditingInterceptor>();

        services.AddDbContext<TrailDbContext>((sp, options) =>
        {
            var db = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseMySQL(db.ConnectionString)
                .AddInterceptors(sp.GetRequiredService<AuditingInterceptor>());
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<TrailDbContext>());

        services.AddHealthChecks().AddDbContextCheck<TrailDbContext>("database", tags: ["ready"]);

        return services;
    }

    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrailDbContext>();
        await db.Database.MigrateAsync(ct);
    }
}
