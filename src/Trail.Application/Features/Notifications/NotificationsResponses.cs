using Trail.Domain.Notifications;

namespace Trail.Application.Features.Notifications;

public sealed record NotificationResponse(
    Guid Id,
    NotificationKind Kind,
    string Title,
    string Body,
    NotificationSubjectType SubjectType,
    Guid SubjectId,
    Guid? SiteId,
    DateTime? ReadAt,
    DateTime CreatedAt);

public sealed record UnreadCountResponse(int Count);

public sealed record MarkedReadResponse(int Count);

public sealed record NotificationPreferencesResponse(
    IReadOnlyList<NotificationKind> EmailKinds,
    bool DigestEnabled,
    int DigestHour);
