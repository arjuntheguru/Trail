using Trail.Api.Infrastructure;
using Trail.Application.Common.Paging;
using Trail.Application.Common.Security;
using Trail.Application.Features.Schedules;

namespace Trail.Api.Endpoints;

internal sealed class ScheduleEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("schedules").WithTags("Schedules").RequireAuthorization(Permissions.Schedules.Manage);

        group.MapGet("", async ([AsParameters] ListSchedulesQuery query, [AsParameters] PageRequest page, ListSchedulesHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.Handle(query, page, ct)));

        group.MapGet("{id:guid}", async (Guid id, GetScheduleHandler handler, CancellationToken ct) =>
            (await handler.Handle(id, ct)).ToHttp());

        group.MapPost("", async (CreateScheduleCommand command, CreateScheduleHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttp(s => TypedResults.Created($"/api/v1/schedules/{s.Id}", s)))
            .Validate<CreateScheduleCommand>();

        group.MapPut("{id:guid}", async (Guid id, UpdateScheduleCommand command, UpdateScheduleHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, command, ct)).ToHttp())
            .Validate<UpdateScheduleCommand>();

        group.MapPost("{id:guid}/deactivate", async (Guid id, SetScheduleActiveHandler handler, CancellationToken ct) =>
            (await handler.Handle(id, false, ct)).ToHttp());

        group.MapPost("{id:guid}/activate", async (Guid id, SetScheduleActiveHandler handler, CancellationToken ct) =>
            (await handler.Handle(id, true, ct)).ToHttp());

        group.MapDelete("{id:guid}", async (Guid id, DeleteScheduleHandler handler, CancellationToken ct) =>
            (await handler.Handle(id, ct)).ToHttp());
    }
}
