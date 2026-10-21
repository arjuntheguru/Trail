using Trail.Api.Infrastructure;
using Trail.Application.Common.Security;
using Trail.Application.Features.Tags;

namespace Trail.Api.Endpoints;

internal sealed class TagEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("tags").WithTags("Tags").RequireAuthorization();

        group.MapGet("", async (ListTagsHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.Handle(ct)))
            .RequireAuthorization(Permissions.Templates.View);

        group.MapPost("", async (CreateTagCommand command, CreateTagHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttp(tag => TypedResults.Created($"/api/v1/tags/{tag.Id}", tag)))
            .Validate<CreateTagCommand>()
            .RequireAuthorization(Permissions.Tags.Manage);

        group.MapPut("{id:guid}", async (Guid id, UpdateTagCommand command, UpdateTagHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, command, ct)).ToHttp())
            .Validate<UpdateTagCommand>()
            .RequireAuthorization(Permissions.Tags.Manage);

        group.MapDelete("{id:guid}", async (Guid id, DeleteTagHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttp())
            .RequireAuthorization(Permissions.Tags.Manage);
    }
}
