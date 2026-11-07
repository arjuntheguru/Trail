using Trail.Api.Infrastructure;
using Trail.Application.Common.Paging;
using Trail.Application.Common.Security;
using Trail.Application.Features.Members;
using Trail.Application.Features.Roles;
using Trail.Domain.Identity;

namespace Trail.Api.Endpoints;

internal sealed class MemberEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var members = app.MapGroup("members").WithTags("Members").RequireAuthorization(Permissions.Members.View);

        members.MapGet("", async (string? search, MembershipStatus? status, Guid? roleId, [AsParameters] PageRequest page,
                ListMembersHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.Handle(new ListMembersQuery(search, status, roleId), page, ct)))
            .Returns<PagedResult<MemberResponse>>();

        members.MapGet("{membershipId:guid}", async (Guid membershipId, GetMemberHandler handler, CancellationToken ct) =>
            (await handler.Handle(membershipId, ct)).ToHttp())
            .Returns<MemberResponse>();

        members.MapPost("invitations", async (InviteMemberCommand command, InviteMemberHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttp(m => TypedResults.Created($"/api/v1/members/{m.Id}", m)))
            .Validate<InviteMemberCommand>()
            .RequireAuthorization(Permissions.Members.Manage)
            .Creates<MemberResponse>()
            .ReturnsConflict();

        members.MapPost("{membershipId:guid}/resend-invitation", async (Guid membershipId, ResendInvitationHandler handler, CancellationToken ct) =>
                (await handler.Handle(membershipId, ct)).ToHttp())
            .RequireAuthorization(Permissions.Members.Manage)
            .ReturnsNoContent()
            .ReturnsConflict();

        members.MapPut("{membershipId:guid}/role", async (Guid membershipId, ChangeMemberRoleCommand command, ManageMemberHandler handler, CancellationToken ct) =>
                (await handler.ChangeRole(membershipId, command, ct)).ToHttp())
            .Validate<ChangeMemberRoleCommand>()
            .RequireAuthorization(Permissions.Members.Manage)
            .Returns<MemberResponse>()
            .ReturnsConflict();

        members.MapPost("{membershipId:guid}/suspend", async (Guid membershipId, ManageMemberHandler handler, CancellationToken ct) =>
                (await handler.Apply(membershipId, MemberAction.Suspend, ct)).ToHttp())
            .RequireAuthorization(Permissions.Members.Manage)
            .ReturnsNoContent()
            .ReturnsConflict();

        members.MapPost("{membershipId:guid}/reactivate", async (Guid membershipId, ManageMemberHandler handler, CancellationToken ct) =>
                (await handler.Apply(membershipId, MemberAction.Reactivate, ct)).ToHttp())
            .RequireAuthorization(Permissions.Members.Manage)
            .ReturnsNoContent()
            .ReturnsConflict();

        members.MapDelete("{membershipId:guid}", async (Guid membershipId, ManageMemberHandler handler, CancellationToken ct) =>
                (await handler.Apply(membershipId, MemberAction.Remove, ct)).ToHttp())
            .RequireAuthorization(Permissions.Members.Manage)
            .ReturnsNoContent()
            .ReturnsConflict();

        var roles = app.MapGroup("roles").WithTags("Roles").RequireAuthorization(Permissions.Members.View);

        roles.MapGet("", async (ListRolesHandler handler, CancellationToken ct) => TypedResults.Ok(await handler.Handle(ct)))
            .Returns<IReadOnlyList<RoleResponse>>();

        roles.MapGet("permissions", () => TypedResults.Ok(ListRolesHandler.Catalog()))
            .Returns<IReadOnlyList<PermissionResponse>>();

        roles.MapPost("", async (SaveRoleCommand command, ManageRoleHandler handler, CancellationToken ct) =>
                (await handler.Create(command, ct)).ToHttp(r => TypedResults.Created($"/api/v1/roles/{r.Id}", r)))
            .Validate<SaveRoleCommand>()
            .RequireAuthorization(Permissions.Roles.Manage)
            .Creates<RoleResponse>()
            .ReturnsConflict();

        roles.MapPut("{roleId:guid}", async (Guid roleId, SaveRoleCommand command, ManageRoleHandler handler, CancellationToken ct) =>
                (await handler.Update(roleId, command, ct)).ToHttp())
            .Validate<SaveRoleCommand>()
            .RequireAuthorization(Permissions.Roles.Manage)
            .Returns<RoleResponse>()
            .ReturnsConflict();

        roles.MapDelete("{roleId:guid}", async (Guid roleId, ManageRoleHandler handler, CancellationToken ct) =>
                (await handler.Delete(roleId, ct)).ToHttp())
            .RequireAuthorization(Permissions.Roles.Manage)
            .ReturnsNoContent()
            .ReturnsConflict();
    }
}