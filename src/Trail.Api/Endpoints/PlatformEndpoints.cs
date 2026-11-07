using Trail.Api.Infrastructure;
using Trail.Application.Common.Paging;
using Trail.Application.Features.Platform;

namespace Trail.Api.Endpoints;

internal sealed class PlatformEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var platform = app.MapGroup("platform/organizations").WithTags("Platform")
            .RequireAuthorization(AuthPolicies.PlatformAdmin);

        platform.MapGet("", async (string? search, [AsParameters] PageRequest page, PlatformOrganizationsHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.List(search, page, ct)))
            .Returns<PagedResult<PlatformOrganizationResponse>>();

        platform.MapPost("", async (CreatePlatformOrganizationCommand command, PlatformOrganizationsHandler handler, CancellationToken ct) =>
                (await handler.Create(command, ct)).ToHttp(o => TypedResults.Created($"/api/v1/platform/organizations/{o.Id}", o)))
            .Validate<CreatePlatformOrganizationCommand>()
            .Creates<PlatformOrganizationResponse>();

        platform.MapPost("{organizationId:guid}/deactivate", async (Guid organizationId, PlatformOrganizationsHandler handler, CancellationToken ct) =>
            (await handler.SetActive(organizationId, false, ct)).ToHttp())
            .ReturnsNoContent();

        platform.MapPost("{organizationId:guid}/activate", async (Guid organizationId, PlatformOrganizationsHandler handler, CancellationToken ct) =>
            (await handler.SetActive(organizationId, true, ct)).ToHttp())
            .ReturnsNoContent();
    }
}