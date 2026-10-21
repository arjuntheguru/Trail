namespace Trail.Application.Features.Notifications;

/// <summary>Links into the frontend app from notification emails.</summary>
public interface INotificationLinks
{
    string Issue(Guid issueId);
    string Task(Guid taskId);
    string NotificationSettings();
}
