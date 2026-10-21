using Trail.Api.Infrastructure;
using Trail.Application.Common.Paging;
using Trail.Application.Features.Notifications;

namespace Trail.Api.Endpoints;

/// <summary>Everything here is about the caller's own membership: there is no permission to manage other people's notifications.</summary>
internal sealed class NotificationEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("notifications").WithTags("Notifications")
            .RequireAuthorization(AuthPolicies.OrganizationMember);

        group.MapGet("", async (
                [AsParameters] ListNotificationsQuery query, [AsParameters] PageRequest page,
                ListNotificationsHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.Handle(query, page, ct)));

        group.MapGet("unread-count", async (GetUnreadCountHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.Handle(ct)));

        group.MapPost("{id:guid}/read", async (Guid id, MarkNotificationsReadHandler handler, CancellationToken ct) =>
            (await handler.MarkRead(id, ct)).ToHttp());

        group.MapPost("read-all", async (MarkNotificationsReadHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.MarkAllRead(ct)));

        group.MapGet("preferences", async (NotificationPreferencesHandler handler, CancellationToken ct) =>
            (await handler.Get(ct)).ToHttp());

        group.MapPut("preferences", async (
                UpdateNotificationPreferencesCommand command, NotificationPreferencesHandler handler, CancellationToken ct) =>
                (await handler.Update(command, ct)).ToHttp())
            .Validate<UpdateNotificationPreferencesCommand>();
    }
}
