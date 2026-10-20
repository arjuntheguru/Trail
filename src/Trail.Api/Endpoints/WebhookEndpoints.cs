using Trail.Api.Infrastructure;
using Trail.Application.Common.Paging;
using Trail.Application.Common.Security;
using Trail.Application.Features.Integrations;

namespace Trail.Api.Endpoints;

internal sealed class WebhookEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("webhooks").WithTags("Webhooks").RequireAuthorization(Permissions.Integrations.Manage);

        group.MapGet("", async (ListWebhooksHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.Handle(ct)));

        group.MapGet("event-types", (ListWebhookEventTypesHandler handler) => TypedResults.Ok(handler.Handle()));

        group.MapPost("", async (CreateWebhookCommand command, CreateWebhookHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttp(created => TypedResults.Created($"/api/v1/webhooks/{created.Webhook.Id}", created)))
            .Validate<CreateWebhookCommand>();

        group.MapGet("{id:guid}", async (Guid id, GetWebhookHandler handler, CancellationToken ct) =>
            (await handler.Handle(id, ct)).ToHttp());

        group.MapPut("{id:guid}", async (Guid id, UpdateWebhookCommand command, UpdateWebhookHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, command, ct)).ToHttp())
            .Validate<UpdateWebhookCommand>();

        group.MapDelete("{id:guid}", async (Guid id, DeleteWebhookHandler handler, CancellationToken ct) =>
            (await handler.Handle(id, ct)).ToHttp());

        group.MapGet("{id:guid}/deliveries", async (Guid id, [AsParameters] PageRequest page, ListWebhookDeliveriesHandler handler, CancellationToken ct) =>
            (await handler.Handle(id, page, ct)).ToHttp());

        group.MapPost("{id:guid}/rotate-secret", async (Guid id, RotateWebhookSecretHandler handler, CancellationToken ct) =>
            (await handler.Handle(id, ct)).ToHttp());

        group.MapPost("{id:guid}/test", async (Guid id, TestWebhookHandler handler, CancellationToken ct) =>
            (await handler.Handle(id, ct)).ToHttp(delivery => TypedResults.Accepted((string?)null, delivery)));

        group.MapPost("deliveries/{id:guid}/redeliver", async (Guid id, RedeliverWebhookHandler handler, CancellationToken ct) =>
            (await handler.Handle(id, ct)).ToHttp());
    }
}
