using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Application.Common.Security;
using Trail.Application.Common.Validation;
using Trail.Domain.Identity;

namespace Trail.Application.Features.Members;

public sealed record InviteMemberCommand(string Email, Guid RoleId, string? FirstName = null, string? LastName = null, bool AllSites = true);

internal sealed class InviteMemberValidator : AbstractValidator<InviteMemberCommand>
{
    public InviteMemberValidator()
    {
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.FirstName).MaximumLength(User.NameMaxLength);
        RuleFor(x => x.LastName).MaximumLength(User.NameMaxLength);
    }
}

/// <summary>
/// Invites someone by email. A new person gets a user without a password; an existing Trail user is linked.
/// Either way the membership stays <see cref="MembershipStatus.Invited"/> until they accept the emailed link.
/// </summary>
public sealed class InviteMemberHandler(IAppDbContext db, ICurrentUser caller, InvitationSender invitations)
{
    public async Task<Result<MemberResponse>> Handle(InviteMemberCommand command, CancellationToken ct)
    {
        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == command.RoleId, ct);
        if (role is null) return Error.Validation("member.unknown_role", "That role does not exist.");
        if (!MemberRules.CallerHolds(caller, role.Permissions)) return MemberRules.Escalation("invite someone to a role");
        if (command.AllSites && !await MemberRules.CallerHasAllSitesAsync(db, caller, ct))
            return Error.Forbidden("member.access_out_of_scope", "You can only invite members to the sites you can access.");

        var normalized = User.Normalize(command.Email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == normalized, ct);
        if (user is not null && await db.Memberships.AnyAsync(m => m.UserId == user.Id, ct))
            return Error.Conflict("member.already_exists", "This person is already a member or has a pending invitation.");

        if (user is null)
        {
            var localPart = command.Email.Trim().Split('@')[0];
            user = new User
            {
                Email = command.Email.Trim(),
                NormalizedEmail = normalized,
                FirstName = string.IsNullOrWhiteSpace(command.FirstName) ? localPart : command.FirstName.Trim(),
                LastName = command.LastName?.Trim() ?? "",
            };
            db.Users.Add(user);
        }

        var membership = new Membership
        {
            UserId = user.Id,
            User = user,
            RoleId = role.Id,
            Role = role,
            Status = MembershipStatus.Invited,
            AllSites = command.AllSites,
        };
        db.Memberships.Add(membership);

        var token = await invitations.CreateAsync(user, membership, ct);
        await db.SaveChangesAsync(ct);
        await invitations.SendAsync(user, token, ct);

        return await db.Memberships.AsNoTracking().Where(m => m.Id == membership.Id)
            .Select(MemberResponse.Projection).SingleAsync(ct);
    }
}

public sealed class ResendInvitationHandler(IAppDbContext db, InvitationSender invitations)
{
    public async Task<Result> Handle(Guid membershipId, CancellationToken ct)
    {
        var membership = await db.Memberships.Include(m => m.User).SingleOrDefaultAsync(m => m.Id == membershipId, ct);
        if (membership is null) return MemberRules.NotFound;
        if (membership.Status != MembershipStatus.Invited)
            return Error.Conflict("member.not_invited", "This member has already accepted their invitation.");

        var token = await invitations.CreateAsync(membership.User, membership, ct);
        await db.SaveChangesAsync(ct);
        await invitations.SendAsync(membership.User, token, ct);
        return Result.Success();
    }
}

/// <summary>Creates invitation tokens (replacing older ones for the same membership) and sends the email.</summary>
public sealed class InvitationSender(IAppDbContext db, ITenantContext tenant, IEmailSender email, IAppLinks links, TimeProvider clock)
{
    public async Task<string> CreateAsync(User user, Membership membership, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await db.UserTokens
            .Where(t => t.MembershipId == membership.Id && t.Purpose == UserTokenPurpose.Invitation && t.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, now), ct);

        var (token, hash) = SecretToken.Create();
        db.UserTokens.Add(new UserToken
        {
            UserId = user.Id,
            MembershipId = membership.Id,
            Purpose = UserTokenPurpose.Invitation,
            TokenHash = hash,
            CreatedAt = now,
            ExpiresAt = now + UserToken.InvitationLifetime,
        });
        return token;
    }

    public async Task SendAsync(User user, string token, CancellationToken ct)
    {
        var organization = await db.Organizations.AsNoTracking()
            .Where(o => o.Id == tenant.OrganizationId).Select(o => o.Name).SingleAsync(ct);
        await email.SendAsync(new EmailMessage(user.Email, $"You're invited to join {organization} on Trail",
            $"Hi {user.FirstName},\n\nYou have been invited to join {organization} on Trail. Accept the invitation here:\n" +
            $"{links.AcceptInvitation(token)}\n\nThe link expires in 7 days."), ct);
    }
}
