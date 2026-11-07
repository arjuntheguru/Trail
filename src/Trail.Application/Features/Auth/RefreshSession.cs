using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Application.Common.Security;

namespace Trail.Application.Features.Auth;

public sealed record RefreshSessionCommand(string RefreshToken);

internal sealed class RefreshSessionValidator : AbstractValidator<RefreshSessionCommand>
{
    public RefreshSessionValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(128);
}

/// <summary>
/// Rotates a refresh token. A token can be used once: presenting a used or revoked token is treated as theft
/// and revokes every token in its family, signing out both the attacker and the victim.
/// </summary>
public sealed class RefreshSessionHandler(IAppDbContext db, SessionIssuer sessions, TimeProvider clock)
{
    private static readonly Error InvalidToken =
        Error.Unauthorized("auth.invalid_refresh_token", "The session has expired. Sign in again.");

    public async Task<Result<AuthSessionResponse>> Handle(RefreshSessionCommand command, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var hash = SecretToken.Hash(command.RefreshToken);
        var token = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null) return InvalidToken;

        if (token.UsedAt is not null || token.RevokedAt is not null)
        {
            await sessions.RevokeFamilyAsync(token.FamilyId, ct);
            return InvalidToken;
        }

        if (token.ExpiresAt <= now) return InvalidToken;

        // Claim the token atomically so two concurrent refreshes cannot both succeed.
        var claimed = await db.RefreshTokens
            .Where(t => t.Id == token.Id && t.UsedAt == null && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, now), ct);
        if (claimed == 0)
        {
            await sessions.RevokeFamilyAsync(token.FamilyId, ct);
            return InvalidToken;
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == token.UserId, ct);
        if (user is null || !user.IsActive || user.SecurityStamp != token.SecurityStamp)
        {
            await sessions.RevokeFamilyAsync(token.FamilyId, ct);
            return InvalidToken;
        }

        var memberships = await sessions.ActiveMembershipsAsync(user.Id, ct);
        ActiveMembership? active = null;
        if (token.OrganizationId is { } organizationId)
        {
            active = memberships.FirstOrDefault(m => m.OrganizationId == organizationId);
            if (active is null)
            {
                await sessions.RevokeFamilyAsync(token.FamilyId, ct);
                return InvalidToken;
            }
        }
        else if (!user.IsPlatformAdmin)
        {
            return InvalidToken;
        }

        var session = sessions.Issue(user, active, memberships, token.FamilyId);
        await db.SaveChangesAsync(ct);
        return session;
    }
}
