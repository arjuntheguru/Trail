using Trail.Api.Infrastructure;
using Trail.Application.Common.Security;
using Trail.Application.Features.Regions;

namespace Trail.Api.Endpoints;

internal sealed class RegionEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("regions").WithTags("Regions").RequireAuthorization();

        group.MapGet("", async (ListRegionsHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.Handle(ct)))
            .RequireAuthorization(Permissions.Sites.View);

        group.MapPost("", async (CreateRegionCommand command, CreateRegionHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttp(region => TypedResults.Created($"/api/v1/regions/{region.Id}", region)))
            .Validate<CreateRegionCommand>()
            .RequireAuthorization(Permissions.Sites.Manage);

        group.MapPut("{id:guid}", async (Guid id, UpdateRegionCommand command, UpdateRegionHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, command, ct)).ToHttp())
            .Validate<UpdateRegionCommand>()
            .RequireAuthorization(Permissions.Sites.Manage);

        group.MapDelete("{id:guid}", async (Guid id, DeleteRegionHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttp())
            .RequireAuthorization(Permissions.Sites.Manage);
    }
}
