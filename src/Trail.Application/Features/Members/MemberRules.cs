using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Application.Common.Security;
using Trail.Domain.Identity;

namespace Trail.Application.Features.Members;

/// <summary>Guards shared by member and role management so nobody can grant more than they hold.</summary>
internal static class MemberRules
{
    public static readonly Error NotFound = Error.NotFound("member.not_found", "Member not found.");

    public static readonly Error CannotManageSelf =
        Error.Forbidden("member.cannot_manage_self", "You cannot change your own membership.");

    public static readonly Error LastOwner =
        Error.Conflict("member.last_owner", "An organization must keep at least one active Owner.");

    /// <summary>True when the caller holds every permission in <paramref name="permissions"/>.</summary>
    public static bool CallerHolds(ICurrentUser caller, IEnumerable<string> permissions) =>
        permissions.All(caller.HasPermission);

    public static Error Escalation(string what) =>
        Error.Forbidden("member.privilege_escalation", $"You cannot {what} with permissions you do not have yourself.");

    public static bool IsOwnerRole(Role role) => role.IsSystem && role.Name == SystemRoles.Owner;

    /// <summary>Would removing <paramref name="membership"/> from the active owners leave none?</summary>
    public static async Task<bool> IsLastActiveOwnerAsync(IAppDbContext db, Membership membership, CancellationToken ct)
    {
        if (membership.Status != MembershipStatus.Active || !IsOwnerRole(membership.Role)) return false;

        var owners = await db.Memberships.CountAsync(m =>
            m.Status == MembershipStatus.Active && m.Role.IsSystem && m.Role.Name == SystemRoles.Owner, ct);
        return owners <= 1;
    }

    public static async Task<bool> CallerHasAllSitesAsync(IAppDbContext db, ICurrentUser caller, CancellationToken ct) =>
        caller.IsPlatformAdmin
        || await db.Memberships.AnyAsync(m => m.Id == caller.MembershipId && m.AllSites, ct);
}
