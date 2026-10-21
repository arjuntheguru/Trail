using Trail.Application.Common.Abstractions;
using Trail.Domain.Notifications;

namespace Trail.Application.Features.Notifications;

/// <summary>Builds the email for one notification. Pure.</summary>
public static class NotificationEmailComposer
{
    public static EmailMessage Compose(Notification notification, string toAddress, string firstName, INotificationLinks links)
    {
        var link = notification.SubjectType == NotificationSubjectType.Issue
            ? links.Issue(notification.SubjectId)
            : links.Task(notification.SubjectId);

        var body = $"Hi {firstName},\n\n{notification.Title}\n\n{notification.Body}\n\nOpen it in Trail:\n{link}\n\n" +
                   $"You can change which notifications are emailed to you here:\n{links.NotificationSettings()}";
        return new EmailMessage(toAddress, notification.Title, body);
    }
}
