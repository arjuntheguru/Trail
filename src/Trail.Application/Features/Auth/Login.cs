using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Identity;

namespace Trail.Application.Features.Auth;

public sealed record LoginCommand(string Email, string Password, Guid? OrganizationId = null);

internal sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(User.EmailMaxLength);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(1024);
    }
}

public sealed class LoginHandler(IAppDbContext db, IPasswordHasher hasher, SessionIssuer sessions, TimeProvider clock)
{
    internal static readonly Error InvalidCredentials =
        Error.Unauthorized("auth.invalid_credentials", "The email or password is incorrect.");

    // Verified against when the email is unknown so both paths cost the same and timing reveals nothing.
    private static string? _dummyHash;

    public async Task<Result<AuthSessionResponse>> Handle(LoginCommand command, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var normalized = User.Normalize(command.Email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == normalized, ct);

        if (user is null)
        {
            var dummy = new User { Email = "", NormalizedEmail = "", FirstName = "", LastName = "" };
            dummy.PasswordHash = _dummyHash ??= hasher.Hash(dummy, Guid.NewGuid().ToString());
            hasher.Verify(dummy, command.Password);
            return InvalidCredentials;
        }

        if (user.IsLockedOut(now))
            return Error.Unauthorized("auth.locked_out", "Too many failed attempts. Try again in a few minutes or reset your password.");

        var check = hasher.Verify(user, command.Password);
        if (check == PasswordCheck.Failed)
        {
            user.RecordFailedSignIn(now);
            await db.SaveChangesAsync(ct);
            return InvalidCredentials;
        }

        if (!user.IsActive)
            return Error.Unauthorized("auth.account_disabled", "This account has been disabled.");

        // Upgrade old hashes in place without rotating the security stamp (other sessions stay valid).
        if (check == PasswordCheck.SuccessRehashNeeded)
            user.PasswordHash = hasher.Hash(user, command.Password);

        var memberships = await sessions.ActiveMembershipsAsync(user.Id, ct);
        ActiveMembership? active;
        if (command.OrganizationId is { } requested)
        {
            active = memberships.FirstOrDefault(m => m.OrganizationId == requested);
            if (active is null)
                return Error.Forbidden("auth.not_a_member", "You are not an active member of that organization.");
        }
        else
        {
            active = memberships.FirstOrDefault(m => m.OrganizationId == user.LastOrganizationId) ?? memberships.FirstOrDefault();
        }

        if (active is null && !user.IsPlatformAdmin)
            return Error.Forbidden("auth.no_organization", "Your account is not an active member of any organization.");

        user.RecordSuccessfulSignIn(now, active?.OrganizationId ?? user.LastOrganizationId);
        var session = sessions.Issue(user, active, memberships);
        await db.SaveChangesAsync(ct);
        return session;
    }
}
