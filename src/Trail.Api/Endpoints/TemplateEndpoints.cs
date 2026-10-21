using Trail.Api.Infrastructure;
using Trail.Application.Common.Paging;
using Trail.Application.Common.Security;
using Trail.Application.Features.Templates;

namespace Trail.Api.Endpoints;

internal sealed class TemplateEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("templates").WithTags("Templates").RequireAuthorization();

        group.MapGet("", async ([AsParameters] ListTemplatesQuery query, [AsParameters] PageRequest page, ListTemplatesHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.Handle(query, page, ct)))
            .RequireAuthorization(Permissions.Templates.View);

        group.MapGet("library", (ListLibraryTemplatesHandler handler) => TypedResults.Ok(handler.Handle()))
            .RequireAuthorization(Permissions.Templates.View);

        group.MapGet("library/{key}", (string key, GetLibraryTemplateHandler handler) => handler.Handle(key).ToHttp())
            .RequireAuthorization(Permissions.Templates.View);

        group.MapPost("library/{key}/copy", async (string key, CopyLibraryTemplateHandler handler, CancellationToken ct) =>
                (await handler.Handle(key, ct)).ToHttp(t => TypedResults.Created($"/api/v1/templates/{t.Id}", t)))
            .RequireAuthorization(Permissions.Templates.Manage);

        group.MapGet("{id:guid}", async (Guid id, GetTemplateHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttp())
            .RequireAuthorization(Permissions.Templates.View);

        group.MapPost("", async (CreateTemplateCommand command, CreateTemplateHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttp(t => TypedResults.Created($"/api/v1/templates/{t.Id}", t)))
            .Validate<CreateTemplateCommand>()
            .RequireAuthorization(Permissions.Templates.Manage);

        group.MapPut("{id:guid}", async (Guid id, UpdateTemplateCommand command, UpdateTemplateHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, command, ct)).ToHttp())
            .Validate<UpdateTemplateCommand>()
            .RequireAuthorization(Permissions.Templates.Manage);

        group.MapPost("{id:guid}/deactivate", async (Guid id, SetTemplateActiveHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, false, ct)).ToHttp())
            .RequireAuthorization(Permissions.Templates.Manage);

        group.MapPost("{id:guid}/activate", async (Guid id, SetTemplateActiveHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, true, ct)).ToHttp())
            .RequireAuthorization(Permissions.Templates.Manage);

        group.MapDelete("{id:guid}", async (Guid id, DeleteTemplateHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttp())
            .RequireAuthorization(Permissions.Templates.Manage);
    }
}
