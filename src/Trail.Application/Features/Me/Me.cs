using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Application.Common.Validation;
using Trail.Application.Features.Auth;
using Trail.Domain.Identity;

namespace Trail.Application.Features.Me;

public sealed record MeResponse(
    UserSummary User,
    OrganizationSummary? Organization,
    IReadOnlyList<OrganizationSummary> Organizations,
    IReadOnlyList<string> Permissions);

public sealed record UpdateProfileCommand(string FirstName, string LastName);

internal sealed class UpdateProfileValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.FirstName).PersonName();
        RuleFor(x => x.LastName).PersonName();
    }
}

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword);

internal sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(1024);
        RuleFor(x => x.NewPassword).Password().NotEqual(x => x.CurrentPassword).WithMessage("Choose a password you have not used here.");
    }
}

public sealed class MeHandler(
    IAppDbContext db,
    ICurrentUser caller,
    IPasswordHasher hasher,
    SessionIssuer sessions,
    EmailConfirmationSender confirmations)
{
    private static readonly Error NotFound = Error.Unauthorized("auth.account_disabled", "This account has been disabled.");

    public async Task<Result<MeResponse>> Get(CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == caller.UserId && u.IsActive, ct);
        if (user is null) return NotFound;

        var memberships = await sessions.ActiveMembershipsAsync(user.Id, ct);
        var active = memberships.FirstOrDefault(m => m.MembershipId == caller.MembershipId);
        return new MeResponse(SessionIssuer.ToSummary(user), active?.ToSummary(),
            memberships.Select(m => m.ToSummary()).ToList(), active?.Permissions ?? []);
    }

    public async Task<Result<UserSummary>> UpdateProfile(UpdateProfileCommand command, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == caller.UserId && u.IsActive, ct);
        if (user is null) return NotFound;

        user.FirstName = command.FirstName.Trim();
        user.LastName = command.LastName.Trim();
        await db.SaveChangesAsync(ct);
        return SessionIssuer.ToSummary(user);
    }

    /// <summary>Changes the password, signs out every other session and returns a fresh one for this device.</summary>
    public async Task<Result<AuthSessionResponse>> ChangePassword(ChangePasswordCommand command, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == caller.UserId && u.IsActive, ct);
        if (user is null) return NotFound;
        if (hasher.Verify(user, command.CurrentPassword) == PasswordCheck.Failed)
            return Error.Validation("auth.wrong_password", "Your current password is incorrect.");

        user.SetPasswordHash(hasher.Hash(user, command.NewPassword));
        await sessions.RevokeAllAsync(user.Id, ct);

        var memberships = await sessions.ActiveMembershipsAsync(user.Id, ct);
        var session = sessions.Issue(user, memberships.FirstOrDefault(m => m.MembershipId == caller.MembershipId), memberships);
        await db.SaveChangesAsync(ct);
        return session;
    }

    public async Task<Result> RevokeAllSessions(CancellationToken ct)
    {
        if (caller.UserId is not { } userId) return NotFound;
        await sessions.RevokeAllAsync(userId, ct);
        return Result.Success();
    }

    public async Task<Result> ResendEmailConfirmation(CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == caller.UserId && u.IsActive, ct);
        if (user is null) return NotFound;
        if (user.EmailConfirmedAt is not null)
            return Error.Conflict("auth.email_already_confirmed", "Your email address is already confirmed.");

        await db.UserTokens
            .Where(t => t.UserId == user.Id && t.Purpose == UserTokenPurpose.EmailConfirmation && t.UsedAt == null)
            .ExecuteDeleteAsync(ct);
        var token = confirmations.Create(user);
        await db.SaveChangesAsync(ct);
        await confirmations.SendAsync(user, token, ct);
        return Result.Success();
    }
}
