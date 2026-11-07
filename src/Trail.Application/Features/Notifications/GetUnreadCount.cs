using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;

namespace Trail.Application.Features.Notifications;

public sealed class GetUnreadCountHandler(IAppDbContext db, ICurrentUser caller)
{
    public async Task<UnreadCountResponse> Handle(CancellationToken ct)
    {
        var mine = caller.MembershipId;
        return new UnreadCountResponse(await db.Notifications.AsNoTracking().CountAsync(n => n.MembershipId == mine && n.ReadAt == null, ct));
    }
}
