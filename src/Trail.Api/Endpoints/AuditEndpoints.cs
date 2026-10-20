using Trail.Api.Infrastructure;
using Trail.Application.Common.Paging;
using Trail.Application.Common.Security;
using Trail.Application.Features.Audit;

namespace Trail.Api.Endpoints;

internal sealed class AuditEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("audit").WithTags("Audit").RequireAuthorization(Permissions.Audit.View);

        group.MapGet("", async (
                    [AsParameters] ListAuditEntriesQuery query, [AsParameters] PageRequest page,
                    ListAuditEntriesHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.Handle(query, page, ct)));
    }
}
