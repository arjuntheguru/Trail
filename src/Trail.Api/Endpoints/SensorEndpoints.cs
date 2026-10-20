using System.Security.Claims;
using Trail.Api.Infrastructure;
using Trail.Application.Common.Paging;
using Trail.Application.Common.Security;
using Trail.Application.Features.Integrations;

namespace Trail.Api.Endpoints;

internal sealed class SensorEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var devices = app.MapGroup("sensor-devices").WithTags("Sensors").RequireAuthorization(Permissions.Integrations.Manage);

        devices.MapGet("", async ([AsParameters] PageRequest page, ListSensorDevicesHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.Handle(page, ct)));

        devices.MapPost("", async (CreateSensorDeviceCommand command, CreateSensorDeviceHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttp(device => TypedResults.Created($"/api/v1/sensor-devices/{device.Id}", device)))
            .Validate<CreateSensorDeviceCommand>();

        devices.MapGet("{id:guid}", async (Guid id, GetSensorDeviceHandler handler, CancellationToken ct) =>
            (await handler.Handle(id, ct)).ToHttp());

        devices.MapPut("{id:guid}", async (Guid id, UpdateSensorDeviceCommand command, UpdateSensorDeviceHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, command, ct)).ToHttp())
            .Validate<UpdateSensorDeviceCommand>();

        devices.MapDelete("{id:guid}", async (Guid id, DeleteSensorDeviceHandler handler, CancellationToken ct) =>
            (await handler.Handle(id, ct)).ToHttp());

        // Machine endpoint: authenticated by API key only (see ApiKeyAuth), never by a user token.
        var sensors = app.MapGroup("sensors").WithTags("Sensors")
            .RequireAuthorization(ApiKeyAuth.SensorsWritePolicy)
            .RequireRateLimiting(RateLimitPolicies.Sensors);

        sensors.MapPost("readings", async (SensorReadingsCommand command, ClaimsPrincipal caller, IngestSensorReadingsHandler handler, CancellationToken ct) =>
                (await handler.Handle(Guid.Parse(caller.FindFirstValue(ApiKeyClaims.ApiKeyId)!), command, ct)).ToHttp())
            .Validate<SensorReadingsCommand>();
    }
}
