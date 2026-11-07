using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;

namespace Trail.Application.Features.Auth;

public sealed record SwitchOrganizationCommand(Guid OrganizationId);

/// <summary>Starts a new session in another organization the caller belongs to.</summary>
public sealed class SwitchOrganizationHandler(IAppDbContext db, ICurrentUser currentUser, SessionIssuer sessions, TimeProvider clock)
{
    public async Task<Result<AuthSessionResponse>> Handle(SwitchOrganizationCommand command, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == currentUser.UserId, ct);
        if (user is not { IsActive: true })
            return Error.Unauthorized("auth.account_disabled", "This account has been disabled.");

        var memberships = await sessions.ActiveMembershipsAsync(user.Id, ct);
        var active = memberships.FirstOrDefault(m => m.OrganizationId == command.OrganizationId);
        if (active is null)
            return Error.Forbidden("auth.not_a_member", "You are not an active member of that organization.");

        user.RecordSuccessfulSignIn(clock.GetUtcNow().UtcDateTime, active.OrganizationId);
        var session = sessions.Issue(user, active, memberships);
        await db.SaveChangesAsync(ct);
        return session;
    }
}
