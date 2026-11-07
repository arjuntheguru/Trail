using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Application.Common.Validation;
using Trail.Domain.Identity;

namespace Trail.Application.Features.Auth;

/// <summary>Names and password are required when the invited person has no Trail account yet.</summary>
public sealed record AcceptInvitationCommand(string Token, string? FirstName = null, string? LastName = null, string? Password = null);

internal sealed class AcceptInvitationValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(128);
        RuleFor(x => x.FirstName!).PersonName().When(x => x.FirstName is not null);
        RuleFor(x => x.LastName!).PersonName().When(x => x.LastName is not null);
        RuleFor(x => x.Password!).Password().When(x => x.Password is not null);
    }
}

public sealed class AcceptInvitationHandler(
    IAppDbContext db,
    ITenantContext tenant,
    IPasswordHasher hasher,
    SessionIssuer sessions,
    TimeProvider clock)
{
    public async Task<Result<AuthSessionResponse>> Handle(AcceptInvitationCommand command, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var token = await UserTokens.RedeemAsync(db, command.Token, UserTokenPurpose.Invitation, now, ct);
        if (token?.MembershipId is not { } membershipId) return UserTokens.InvalidToken;

        // The invitee is not signed in, so no organization is active yet; the token identifies the membership.
        var membership = await db.Memberships
            .IgnoreQueryFilters([QueryFilters.Tenant])
            .Include(m => m.User)
            .SingleOrDefaultAsync(m => m.Id == membershipId && m.UserId == token.UserId, ct);
        if (membership is not { Status: MembershipStatus.Invited } || !membership.User.IsActive)
            return UserTokens.InvalidToken;

        var user = membership.User;
        if (user.PasswordHash is null)
        {
            if (command.Password is null || string.IsNullOrWhiteSpace(command.FirstName) || command.LastName is null)
                return Error.Validation("auth.profile_required", "Enter your name and choose a password to finish joining.");
            user.FirstName = command.FirstName.Trim();
            user.LastName = command.LastName.Trim();
            user.SetPasswordHash(hasher.Hash(user, command.Password));
        }

        tenant.UseOrganization(membership.OrganizationId);
        membership.Status = MembershipStatus.Active;
        membership.JoinedAt = now;
        user.EmailConfirmedAt ??= now;
        user.RecordSuccessfulSignIn(now, membership.OrganizationId);
        await db.SaveChangesAsync(ct);

        var memberships = await sessions.ActiveMembershipsAsync(user.Id, ct);
        var session = sessions.Issue(user, memberships.Single(m => m.MembershipId == membership.Id), memberships);
        await db.SaveChangesAsync(ct);
        return session;
    }
}
