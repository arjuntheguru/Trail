using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Application.Common.Security;
using Trail.Application.Features.Members;
using Trail.Domain.Identity;

namespace Trail.Application.Features.Roles;

public sealed record RoleResponse(Guid Id, string Name, string? Description, bool IsSystem, IReadOnlyList<string> Permissions, int MemberCount);

public sealed record PermissionResponse(string Key, string Group);

public sealed record SaveRoleCommand(string Name, string? Description, IReadOnlyList<string> Permissions);

internal sealed class SaveRoleValidator : AbstractValidator<SaveRoleCommand>
{
    public SaveRoleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Role.NameMaxLength);
        RuleFor(x => x.Description).MaximumLength(Role.DescriptionMaxLength);
        RuleFor(x => x.Permissions).NotNull();
        RuleForEach(x => x.Permissions).Must(Common.Security.Permissions.IsKnown).WithMessage("'{PropertyValue}' is not a known permission.");
    }
}

public sealed class ListRolesHandler(IAppDbContext db)
{
    public async Task<IReadOnlyList<RoleResponse>> Handle(CancellationToken ct)
    {
        var roles = await db.Roles.AsNoTracking()
            .OrderByDescending(r => r.IsSystem).ThenBy(r => r.Name)
            .Select(r => new { Role = r, MemberCount = db.Memberships.Count(m => m.RoleId == r.Id) })
            .ToListAsync(ct);
        return roles.Select(x => ToResponse(x.Role, x.MemberCount)).ToList();
    }

    public static IReadOnlyList<PermissionResponse> Catalog() =>
        Common.Security.Permissions.All.Select(p => new PermissionResponse(p, p.Split('.')[0])).ToList();

    internal static RoleResponse ToResponse(Role role, int memberCount) =>
        new(role.Id, role.Name, role.Description, role.IsSystem, role.Permissions.Order(StringComparer.Ordinal).ToList(), memberCount);
}

/// <summary>
/// Custom roles. A caller can only put permissions they hold themselves into a role, and can only change or
/// delete roles whose current permissions they hold. The Owner role is fixed; system roles cannot be renamed.
/// </summary>
public sealed class ManageRoleHandler(IAppDbContext db, ICurrentUser caller)
{
    private static readonly Error NotFound = Error.NotFound("role.not_found", "Role not found.");

    public async Task<Result<RoleResponse>> Create(SaveRoleCommand command, CancellationToken ct)
    {
        if (!MemberRules.CallerHolds(caller, command.Permissions)) return MemberRules.Escalation("create a role");
        var name = command.Name.Trim();
        if (await db.Roles.AnyAsync(r => r.Name == name, ct)) return NameTaken;

        var role = new Role { Name = name, Description = command.Description?.Trim(), Permissions = Distinct(command.Permissions) };
        db.Roles.Add(role);
        await db.SaveChangesAsync(ct);
        return ListRolesHandler.ToResponse(role, 0);
    }

    public async Task<Result<RoleResponse>> Update(Guid roleId, SaveRoleCommand command, CancellationToken ct)
    {
        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == roleId, ct);
        if (role is null) return NotFound;
        if (MemberRules.IsOwnerRole(role)) return Error.Forbidden("role.owner_immutable", "The Owner role cannot be changed.");
        if (!MemberRules.CallerHolds(caller, role.Permissions) || !MemberRules.CallerHolds(caller, command.Permissions))
            return MemberRules.Escalation("change a role");

        var name = command.Name.Trim();
        if (role.IsSystem && name != role.Name)
            return Error.Forbidden("role.system_rename", "Built-in roles cannot be renamed.");
        if (name != role.Name && await db.Roles.AnyAsync(r => r.Name == name && r.Id != roleId, ct)) return NameTaken;

        role.Name = name;
        role.Description = command.Description?.Trim();
        role.Permissions = Distinct(command.Permissions);
        await db.SaveChangesAsync(ct);
        return ListRolesHandler.ToResponse(role, await db.Memberships.CountAsync(m => m.RoleId == roleId, ct));
    }

    public async Task<Result> Delete(Guid roleId, CancellationToken ct)
    {
        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == roleId, ct);
        if (role is null) return NotFound;
        if (role.IsSystem) return Error.Forbidden("role.system_delete", "Built-in roles cannot be deleted.");
        if (!MemberRules.CallerHolds(caller, role.Permissions)) return MemberRules.Escalation("delete a role");
        if (await db.Memberships.AnyAsync(m => m.RoleId == roleId, ct))
            return Error.Conflict("role.in_use", "Move the members of this role to another role first.");

        db.Roles.Remove(role);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static readonly Error NameTaken = Error.Conflict("role.name_taken", "A role with this name already exists.");

    private static List<string> Distinct(IEnumerable<string> permissions) =>
        permissions.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
}
