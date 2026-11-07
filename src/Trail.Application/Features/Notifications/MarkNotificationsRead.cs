using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;

namespace Trail.Application.Features.Notifications;

public sealed class MarkNotificationsReadHandler(IAppDbContext db, ICurrentUser caller, TimeProvider clock)
{
    private static readonly Error NotFound = Error.NotFound("notification.not_found", "Notification not found.");

    /// <summary>Marks one of the caller's notifications read. Someone else's notification is reported as not found.</summary>
    public async Task<Result> MarkRead(Guid id, CancellationToken ct)
    {
        var mine = caller.MembershipId;
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.MembershipId == mine, ct);
        if (notification is null) return NotFound;

        if (notification.ReadAt is null)
        {
            notification.ReadAt = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
        }
        return Result.Success();
    }

    public async Task<MarkedReadResponse> MarkAllRead(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var mine = caller.MembershipId;
        // Bulk update: the auditing interceptor does not see it, so stamp the audit columns here.
        var count = await db.Notifications
            .Where(n => n.MembershipId == mine && n.ReadAt == null)
            .ExecuteUpdateAsync(u => u
                .SetProperty(n => n.ReadAt, now)
                .SetProperty(n => n.UpdatedAt, now)
                .SetProperty(n => n.UpdatedById, caller.UserId)
                .SetProperty(n => n.ConcurrencyStamp, Guid.NewGuid()), ct);
        return new MarkedReadResponse(count);
    }
}
