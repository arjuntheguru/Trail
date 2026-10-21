using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Paging;

namespace Trail.Application.Features.Notifications;

public sealed record ListNotificationsQuery(bool UnreadOnly = false);

public sealed class ListNotificationsHandler(IAppDbContext db, ICurrentUser caller)
{
    /// <summary>The caller's own notifications, newest first.</summary>
    public async Task<PagedResult<NotificationResponse>> Handle(ListNotificationsQuery query, PageRequest page, CancellationToken ct)
    {
        var mine = caller.MembershipId;
        var notifications = db.Notifications.AsNoTracking().Where(n => n.MembershipId == mine);
        if (query.UnreadOnly) notifications = notifications.Where(n => n.ReadAt == null);

        return await notifications
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Select(n => new NotificationResponse(
                n.Id, n.Kind, n.Title, n.Body, n.SubjectType, n.SubjectId, n.SiteId, n.ReadAt, n.CreatedAt))
            .ToPagedResultAsync(page, ct);
    }
}
