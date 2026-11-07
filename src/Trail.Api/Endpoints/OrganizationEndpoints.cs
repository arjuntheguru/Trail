using Trail.Api.Infrastructure;
using Trail.Application.Common.Security;
using Trail.Application.Features.Organizations;

namespace Trail.Api.Endpoints;

internal sealed class OrganizationEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("organizations/current").WithTags("Organizations").RequireAuthorization();

        group.MapGet("", async (GetCurrentOrganizationHandler handler, CancellationToken ct) =>
                (await handler.Handle(ct)).ToHttp())
            .RequireAuthorization(AuthPolicies.OrganizationMember);

        group.MapPut("", async (UpdateCurrentOrganizationCommand command, UpdateCurrentOrganizationHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttp())
            .Validate<UpdateCurrentOrganizationCommand>()
            .RequireAuthorization(Permissions.Organization.Manage);
    }
}
