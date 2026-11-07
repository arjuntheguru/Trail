using Trail.Api.Infrastructure;
using Trail.Application.Common.Security;
using Trail.Application.Features.Members;

namespace Trail.Api.Endpoints;

internal sealed class MemberAccessEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("members/{membershipId:guid}/access").WithTags("Members").RequireAuthorization();

        group.MapGet("", async (Guid membershipId, GetMemberAccessHandler handler, CancellationToken ct) =>
                (await handler.Handle(membershipId, ct)).ToHttp())
            .RequireAuthorization(Permissions.Members.View);

        group.MapPut("", async (Guid membershipId, SetMemberAccessCommand command, SetMemberAccessHandler handler, CancellationToken ct) =>
                (await handler.Handle(membershipId, command, ct)).ToHttp())
            .Validate<SetMemberAccessCommand>()
            .RequireAuthorization(Permissions.Members.Manage);
    }
}
