using Microsoft.AspNetCore.Mvc;
using Trail.Api.Infrastructure;
using Trail.Application.Common.Paging;
using Trail.Application.Common.Security;
using Trail.Application.Features.Issues;

namespace Trail.Api.Endpoints;

internal sealed class IssueEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("issues").WithTags("Issues").RequireAuthorization();

        group.MapGet("", async ([AsParameters] ListIssuesQuery query, [AsParameters] PageRequest page, ListIssuesHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.Handle(query, page, ct)))
            .RequireAuthorization(Permissions.Issues.View);

        group.MapGet("summary", async ([AsParameters] GetIssuesSummaryQuery query, GetIssuesSummaryHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.Handle(query, ct)))
            .RequireAuthorization(Permissions.Issues.View);

        group.MapGet("{id:guid}", async (Guid id, GetIssueHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttp())
            .RequireAuthorization(Permissions.Issues.View);

        group.MapPost("", async (RaiseIssueCommand command, RaiseIssueHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttp(issue => TypedResults.Created($"/api/v1/issues/{issue.Id}", issue)))
            .Validate<RaiseIssueCommand>()
            .RequireAuthorization(Permissions.Issues.Raise);

        group.MapPost("{id:guid}/comments", async (Guid id, CommentOnIssueCommand command, CommentOnIssueHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, command, ct)).ToHttp())
            .Validate<CommentOnIssueCommand>()
            .RequireAuthorization(Permissions.Issues.Raise);

        group.MapPut("{id:guid}/assignee", async (Guid id, AssignIssueCommand command, ChangeIssueHandler handler, CancellationToken ct) =>
                (await handler.Assign(id, command, ct)).ToHttp())
            .Validate<AssignIssueCommand>()
            .RequireAuthorization(Permissions.Issues.Manage);

        group.MapPut("{id:guid}/priority", async (Guid id, ChangeIssuePriorityCommand command, ChangeIssueHandler handler, CancellationToken ct) =>
                (await handler.ChangePriority(id, command, ct)).ToHttp())
            .Validate<ChangeIssuePriorityCommand>()
            .RequireAuthorization(Permissions.Issues.Manage);

        group.MapPut("{id:guid}/due", async (Guid id, SetIssueDueCommand command, ChangeIssueHandler handler, CancellationToken ct) =>
                (await handler.SetDue(id, command, ct)).ToHttp())
            .Validate<SetIssueDueCommand>()
            .RequireAuthorization(Permissions.Issues.Manage);

        // Start and resolve: the assignee (or a manager) may act. The handler decides; the policy only needs Raise.
        group.MapPost("{id:guid}/start", async (Guid id, IssueWorkflowHandler handler, CancellationToken ct) =>
                (await handler.Start(id, ct)).ToHttp())
            .RequireAuthorization(Permissions.Issues.Raise);

        group.MapPost("{id:guid}/resolve", async (Guid id, ResolveIssueCommand command, IssueWorkflowHandler handler, CancellationToken ct) =>
                (await handler.Resolve(id, command, ct)).ToHttp())
            .Validate<ResolveIssueCommand>()
            .RequireAuthorization(Permissions.Issues.Raise);

        // The note is optional, so the body may be absent; the handler checks its length.
        group.MapPost("{id:guid}/close", async (Guid id, [FromBody] CloseIssueCommand? command, IssueWorkflowHandler handler, CancellationToken ct) =>
                (await handler.Close(id, command, ct)).ToHttp())
            .RequireAuthorization(Permissions.Issues.Manage);

        group.MapPost("{id:guid}/reopen", async (Guid id, ReopenIssueCommand command, IssueWorkflowHandler handler, CancellationToken ct) =>
                (await handler.Reopen(id, command, ct)).ToHttp())
            .Validate<ReopenIssueCommand>()
            .RequireAuthorization(Permissions.Issues.Manage);
    }
}
