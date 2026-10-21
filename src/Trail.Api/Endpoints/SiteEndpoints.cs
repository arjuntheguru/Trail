using Microsoft.AspNetCore.Mvc;
using Trail.Api.Infrastructure;
using Trail.Application.Common.Paging;
using Trail.Application.Common.Security;
using Trail.Application.Features.Sites;

namespace Trail.Api.Endpoints;

internal sealed class SiteEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("sites").WithTags("Sites").RequireAuthorization();

        group.MapGet("", async ([AsParameters] ListSitesQuery query, [AsParameters] PageRequest page, ListSitesHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.Handle(query, page, ct)))
            .RequireAuthorization(Permissions.Sites.View);

        group.MapGet("{id:guid}", async (Guid id, GetSiteHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttp())
            .RequireAuthorization(Permissions.Sites.View);

        group.MapPost("", async (CreateSiteCommand command, CreateSiteHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttp(site => TypedResults.Created($"/api/v1/sites/{site.Id}", site)))
            .Validate<CreateSiteCommand>()
            .RequireAuthorization(Permissions.Sites.Manage);

        group.MapPut("{id:guid}", async (Guid id, UpdateSiteCommand command, UpdateSiteHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, command, ct)).ToHttp())
            .Validate<UpdateSiteCommand>()
            .RequireAuthorization(Permissions.Sites.Manage);

        group.MapPost("{id:guid}/deactivate", async (Guid id, SetSiteActiveHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, false, ct)).ToHttp())
            .RequireAuthorization(Permissions.Sites.Manage);

        group.MapPost("{id:guid}/activate", async (Guid id, SetSiteActiveHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, true, ct)).ToHttp())
            .RequireAuthorization(Permissions.Sites.Manage);

        group.MapDelete("{id:guid}", async (Guid id, DeleteSiteHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttp())
            .RequireAuthorization(Permissions.Sites.Manage);
    }
}
