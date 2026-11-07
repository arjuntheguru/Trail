using Trail.Api.Infrastructure;
using Trail.Application.Common.Security;
using Trail.Application.Features.Departments;

namespace Trail.Api.Endpoints;

internal sealed class DepartmentEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("departments").WithTags("Departments").RequireAuthorization();

        group.MapGet("", async (ListDepartmentsHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.Handle(ct)))
            .RequireAuthorization(Permissions.Sites.View);

        group.MapPost("", async (CreateDepartmentCommand command, CreateDepartmentHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttp(department => TypedResults.Created($"/api/v1/departments/{department.Id}", department)))
            .Validate<CreateDepartmentCommand>()
            .RequireAuthorization(Permissions.Sites.Manage);

        group.MapPut("{id:guid}", async (Guid id, UpdateDepartmentCommand command, UpdateDepartmentHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, command, ct)).ToHttp())
            .Validate<UpdateDepartmentCommand>()
            .RequireAuthorization(Permissions.Sites.Manage);

        group.MapDelete("{id:guid}", async (Guid id, DeleteDepartmentHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttp())
            .RequireAuthorization(Permissions.Sites.Manage);
    }
}
