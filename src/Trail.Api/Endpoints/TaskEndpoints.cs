using Microsoft.AspNetCore.Mvc;
using Trail.Api.Infrastructure;
using Trail.Application.Common.Paging;
using Trail.Application.Common.Security;
using Trail.Application.Features.Tasks;

namespace Trail.Api.Endpoints;

internal sealed class TaskEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var siteTasks = app.MapGroup("sites/{siteId:guid}/tasks").WithTags("Tasks").RequireAuthorization();

        siteTasks.MapGet("", async (Guid siteId, [AsParameters] ListSiteTasksQuery query, ListSiteTasksHandler handler, CancellationToken ct) =>
                (await handler.Handle(siteId, query, ct)).ToHttp())
            .RequireAuthorization(Permissions.Tasks.View);

        siteTasks.MapPost("", async (Guid siteId, CreateAdHocTaskCommand command, CreateAdHocTaskHandler handler, CancellationToken ct) =>
                (await handler.Handle(siteId, command, ct)).ToHttp(task => TypedResults.Created($"/api/v1/tasks/{task.Id}", task)))
            .Validate<CreateAdHocTaskCommand>()
            .RequireAuthorization(Permissions.Tasks.CreateAdHoc);

        var group = app.MapGroup("tasks").WithTags("Tasks").RequireAuthorization();

        group.MapGet("", async ([AsParameters] ListTasksQuery query, [AsParameters] PageRequest page, ListTasksHandler handler, CancellationToken ct) =>
                (await handler.Handle(query, page, ct)).ToHttp())
            .RequireAuthorization(Permissions.Tasks.View);

        group.MapGet("{id:guid}", async (Guid id, GetTaskHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttp())
            .RequireAuthorization(Permissions.Tasks.View);

        group.MapPut("{id:guid}/progress", async (Guid id, SaveTaskProgressCommand command, SaveTaskProgressHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, command, ct)).ToHttp())
            .Validate<SaveTaskProgressCommand>()
            .RequireAuthorization(Permissions.Tasks.Complete);

        group.MapPost("{id:guid}/entries", async (Guid id, AddTaskEntryCommand command, AddTaskEntryHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, command, ct)).ToHttp(task => TypedResults.Created($"/api/v1/tasks/{task.Id}", task)))
            .Validate<AddTaskEntryCommand>()
            .RequireAuthorization(Permissions.Tasks.Complete);

        group.MapPost("{id:guid}/complete", async (Guid id, CompleteTaskCommand command, CompleteTaskHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, command, ct)).ToHttp())
            .Validate<CompleteTaskCommand>()
            .RequireAuthorization(Permissions.Tasks.Complete);

        // The note is optional, so the whole body may be left out; the handler checks its length.
        group.MapPost("{id:guid}/approve", async (Guid id, [FromBody] ApproveTaskCommand? command, ApproveTaskHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, command, ct)).ToHttp())
            .RequireAuthorization(Permissions.Tasks.Approve);

        group.MapPost("{id:guid}/reopen", async (Guid id, ReopenTaskCommand command, ReopenTaskHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, command, ct)).ToHttp())
            .Validate<ReopenTaskCommand>()
            .RequireAuthorization(Permissions.Tasks.Approve);

        group.MapPost("{id:guid}/snooze", async (Guid id, SnoozeTaskCommand command, SnoozeTaskHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, command, ct)).ToHttp())
            .Validate<SnoozeTaskCommand>()
            .RequireAuthorization(Permissions.Tasks.Complete);
    }
}
