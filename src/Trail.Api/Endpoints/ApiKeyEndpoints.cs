using Trail.Api.Infrastructure;
using Trail.Application.Common.Security;
using Trail.Application.Features.Integrations;

namespace Trail.Api.Endpoints;

internal sealed class ApiKeyEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api-keys").WithTags("API keys").RequireAuthorization(Permissions.Integrations.Manage);

        group.MapGet("", async (ListApiKeysHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.Handle(ct)));

        // The response carries the full key; it is shown this once and only its hash is stored.
        group.MapPost("", async (CreateApiKeyCommand command, CreateApiKeyHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttp(created => TypedResults.Created($"/api/v1/api-keys/{created.ApiKey.Id}", created)))
            .Validate<CreateApiKeyCommand>();

        group.MapPost("{id:guid}/revoke", async (Guid id, RevokeApiKeyHandler handler, CancellationToken ct) =>
            (await handler.Handle(id, ct)).ToHttp());
    }
}
