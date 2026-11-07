using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Application.Common.Settings;
using Trail.Application.Common.Validation;
using Trail.Application.Features.Organizations;
using Trail.Domain.Identity;
using Trail.Domain.Organizations;

namespace Trail.Application.Features.Auth;

public sealed record RegisterOrganizationCommand(
    string OrganizationName,
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string? TimeZone = null);

internal sealed class RegisterOrganizationValidator : AbstractValidator<RegisterOrganizationCommand>
{
    public RegisterOrganizationValidator()
    {
        RuleFor(x => x.OrganizationName).NotEmpty().MaximumLength(Organization.NameMaxLength);
        RuleFor(x => x.FirstName).PersonName();
        RuleFor(x => x.LastName).PersonName();
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.Password).Password();
        RuleFor(x => x.TimeZone).TimeZone();
    }
}

/// <summary>Self-service sign-up: creates the organization and its Owner, then signs the owner in.</summary>
public sealed class RegisterOrganizationHandler(
    IAppDbContext db,
    IPasswordHasher hasher,
    OrganizationProvisioner provisioner,
    SessionIssuer sessions,
    EmailConfirmationSender confirmations,
    IOptions<AppOptions> options,
    TimeProvider clock)
{
    public async Task<Result<AuthSessionResponse>> Handle(RegisterOrganizationCommand command, CancellationToken ct)
    {
        if (!options.Value.AllowSelfSignup)
            return Error.Forbidden("auth.signup_disabled", "Sign-up is by invitation only.");

        var normalized = User.Normalize(command.Email);
        if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalized, ct))
            return Error.Conflict("auth.email_taken", "An account with this email already exists. Sign in instead.");

        var user = new User
        {
            Email = command.Email.Trim(),
            NormalizedEmail = normalized,
            FirstName = command.FirstName.Trim(),
            LastName = command.LastName.Trim(),
        };
        user.SetPasswordHash(hasher.Hash(user, command.Password));
        db.Users.Add(user);

        var (organization, membership) = await provisioner.ProvisionAsync(command.OrganizationName, user, command.TimeZone, ct);
        user.RecordSuccessfulSignIn(clock.GetUtcNow().UtcDateTime, organization.Id);

        var active = new ActiveMembership(membership.Id, organization.Id, organization.Name, organization.Slug,
            membership.Role.Name, membership.Role.Permissions);
        var session = sessions.Issue(user, active, [active]);
        var confirmationToken = confirmations.Create(user);

        await db.SaveChangesAsync(ct);
        await confirmations.SendAsync(user, confirmationToken, ct);
        return session;
    }
}
