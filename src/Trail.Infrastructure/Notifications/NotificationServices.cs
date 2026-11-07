using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Trail.Application.Common.Settings;
using Trail.Application.Features.Notifications;

namespace Trail.Infrastructure.Notifications;

internal sealed class NotificationLinks(IOptions<AppOptions> options) : INotificationLinks
{
    private string Base => options.Value.FrontendBaseUrl.TrimEnd('/');

    public string Issue(Guid issueId) => $"{Base}/issues/{issueId}";
    public string Task(Guid taskId) => $"{Base}/tasks/{taskId}";
    public string NotificationSettings() => $"{Base}/settings/notifications";
}

internal static class NotificationServiceRegistration
{
    public static IServiceCollection AddNotifications(this IServiceCollection services)
    {
        services.AddSingleton<INotificationLinks, NotificationLinks>();
        services.AddScoped<NotificationWriter>();
        services.AddScoped<MembershipDirectory>();
        services.AddScoped<NotificationEmailRunner>();
        services.AddScoped<IssueEscalationRunner>();
        services.AddScoped<DailyDigestRunner>();

        services.AddOptions<NotificationEmailOptions>().BindConfiguration(NotificationEmailOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<IssueEscalationOptions>().BindConfiguration(IssueEscalationOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<DailyDigestOptions>().BindConfiguration(DailyDigestOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();

        services.AddSingleton<NotificationEmailJob>();
        services.AddHostedService(sp => sp.GetRequiredService<NotificationEmailJob>());
        services.AddSingleton<IssueEscalationJob>();
        services.AddHostedService(sp => sp.GetRequiredService<IssueEscalationJob>());
        services.AddSingleton<DailyDigestJob>();
        services.AddHostedService(sp => sp.GetRequiredService<DailyDigestJob>());
        return services;
    }
}
