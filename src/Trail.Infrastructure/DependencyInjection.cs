using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Settings;
using Trail.Infrastructure.Files;
using Trail.Infrastructure.Jobs;
using Trail.Infrastructure.Notifications;
using Trail.Infrastructure.Persistence;
using Trail.Infrastructure.Persistence.Interceptors;
using Trail.Infrastructure.Persistence.Sequences;
using Trail.Infrastructure.Security;
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
        services.AddScoped<ISequenceAllocator, SequenceAllocator>();

        services.AddHealthChecks().AddDbContextCheck<TrailDbContext>("database", tags: ["ready"]);

        AddSecurity(services);
        services.AddNotifications();

        services.AddOptions<StorageOptions>().BindConfiguration(StorageOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        services.AddOptions<TaskGenerationOptions>().BindConfiguration(TaskGenerationOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<TaskGenerationJob>();
        services.AddHostedService(sp => sp.GetRequiredService<TaskGenerationJob>());

        return services;
    }

    private static void AddSecurity(IServiceCollection services)
    {
        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<AppOptions>().BindConfiguration(AppOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<BootstrapOptions>().BindConfiguration(BootstrapOptions.SectionName).ValidateDataAnnotations();
        services.AddOptions<EmailOptions>().BindConfiguration(EmailOptions.SectionName).ValidateDataAnnotations()
            .Validate(o => o.Provider is "Log" or "Smtp", "Email:Provider must be 'Log' or 'Smtp'.")
            .Validate(o => o.Provider != "Smtp" || !string.IsNullOrWhiteSpace(o.SmtpHost), "Email:SmtpHost is required for SMTP.")
            .ValidateOnStart();

        services.AddSingleton<IAccessTokenIssuer, AccessTokenIssuer>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddSingleton<IAppLinks, AppLinks>();
        services.AddSingleton<LogEmailSender>();
        services.AddSingleton<SmtpEmailSender>();
        services.AddSingleton<IEmailSender>(sp =>
            sp.GetRequiredService<IOptions<EmailOptions>>().Value.Provider == "Smtp"
                ? sp.GetRequiredService<SmtpEmailSender>()
                : sp.GetRequiredService<LogEmailSender>());
    }

    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrailDbContext>();
        await db.Database.MigrateAsync(ct);
    }
}
