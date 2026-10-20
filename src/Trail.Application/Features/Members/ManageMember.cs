using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Application.Features.Auth;
using Trail.Domain.Identity;

namespace Trail.Application.Features.Members;

public sealed record ChangeMemberRoleCommand(Guid RoleId);

internal sealed class ChangeMemberRoleValidator : AbstractValidator<ChangeMemberRoleCommand>
{
    public ChangeMemberRoleValidator() => RuleFor(x => x.RoleId).NotEmpty();
}

public enum MemberAction
{
    Suspend,
    Reactivate,
    Remove,
}

/// <summary>
/// Role changes, suspension and removal. The caller may never act on themselves, on someone whose role holds
/// permissions the caller lacks, or in a way that leaves the organization without an active Owner.
/// </summary>
public sealed class ManageMemberHandler(IAppDbContext db, ICurrentUser caller, SessionIssuer sessions)
{
    public async Task<Result<MemberResponse>> ChangeRole(Guid membershipId, ChangeMemberRoleCommand command, CancellationToken ct)
    {
        var (membership, error) = await LoadManageableAsync(membershipId, ct);
        if (error is not null) return error;

        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == command.RoleId, ct);
        if (role is null) return Error.Validation("member.unknown_role", "That role does not exist.");
        if (!MemberRules.CallerHolds(caller, role.Permissions)) return MemberRules.Escalation("assign a role");

        if (role.Id != membership!.RoleId && await MemberRules.IsLastActiveOwnerAsync(db, membership, ct))
            return MemberRules.LastOwner;

        membership.RoleId = role.Id;
        membership.Role = role;
        await db.SaveChangesAsync(ct);
        // New permissions apply from the member's next sign-in or token refresh.
        return await ProjectAsync(membership.Id, ct);
    }

    public async Task<Result> Apply(Guid membershipId, MemberAction action, CancellationToken ct)
    {
        var (membership, error) = await LoadManageableAsync(membershipId, ct);
        if (error is not null) return error;

        if ((action is MemberAction.Suspend or MemberAction.Remove) && await MemberRules.IsLastActiveOwnerAsync(db, membership!, ct))
            return MemberRules.LastOwner;

        switch (action)
        {
            case MemberAction.Suspend:
                if (membership!.Status != MembershipStatus.Active)
                    return Error.Conflict("member.not_active", "Only active members can be suspended.");
                membership.Status = MembershipStatus.Suspended;
                break;
            case MemberAction.Reactivate:
                if (membership!.Status != MembershipStatus.Suspended)
                    return Error.Conflict("member.not_suspended", "Only suspended members can be reactivated.");
                membership.Status = MembershipStatus.Active;
                break;
            case MemberAction.Remove:
                db.Memberships.Remove(membership!);
                break;
        }

        await db.SaveChangesAsync(ct);
        if (action is not MemberAction.Reactivate)
            await sessions.RevokeAllAsync(membership!.UserId, ct, membership.OrganizationId);
        return Result.Success();
    }

    private async Task<(Membership? Membership, Error? Error)> LoadManageableAsync(Guid membershipId, CancellationToken ct)
    {
        var membership = await db.Memberships.Include(m => m.Role).SingleOrDefaultAsync(m => m.Id == membershipId, ct);
        if (membership is null) return (null, MemberRules.NotFound);
        if (membership.Id == caller.MembershipId || membership.UserId == caller.UserId) return (null, MemberRules.CannotManageSelf);
        if (!MemberRules.CallerHolds(caller, membership.Role.Permissions)) return (null, MemberRules.Escalation("manage a member"));
        return (membership, null);
    }

    private async Task<Result<MemberResponse>> ProjectAsync(Guid membershipId, CancellationToken ct) =>
        await db.Memberships.AsNoTracking().Where(m => m.Id == membershipId).Select(MemberResponse.Projection).SingleAsync(ct);
}
