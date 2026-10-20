using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Trail.Api.Infrastructure;
using Trail.Application.Common.Security;
using Trail.Application.Features.Attachments;
using Trail.Domain.Files;

namespace Trail.Api.Endpoints;

internal sealed class AttachmentEndpoints : IEndpointGroup
{
    /// <summary>Room for the multipart framing around a maximum-size file.</summary>
    private const long RequestLimitBytes = Attachment.MaxBytes + 1024 * 1024;

    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("attachments").WithTags("Attachments").RequireAuthorization();

        // Anyone who can fill in tasks or raise issues can attach evidence; no permission of its own.
        group.MapPost("", async (
                    IFormFile file, [FromForm] AttachmentKind kind, [FromForm] Guid siteId,
                    UploadAttachmentHandler handler, CancellationToken ct) =>
                {
                    await using var content = file.OpenReadStream();
                    var result = await handler.Handle(new UploadAttachmentCommand(content, file.FileName, kind, siteId), ct);
                    return result.ToHttp(attachment => TypedResults.Created($"/api/v1/attachments/{attachment.Id}", attachment));
                })
            .RequireAuthorization(policy => policy
                .RequireClaim(TrailClaimTypes.OrganizationId)
                .RequireAssertion(context =>
                    context.User.HasClaim(TrailClaimTypes.Permission, Permissions.Tasks.Complete)
                    || context.User.HasClaim(TrailClaimTypes.Permission, Permissions.Issues.Raise)))
            .WithMetadata(new RequestSizeLimitAttribute(RequestLimitBytes))
            .DisableAntiforgery(); // Bearer-token API: there is no cookie session to forge requests against.

        group.MapGet("{id:guid}", async (Guid id, GetAttachmentHandler handler, CancellationToken ct) =>
                (await handler.Handle(id, ct)).ToHttp())
            .RequireAuthorization(AuthPolicies.OrganizationMember);

        group.MapGet("{id:guid}/content", async Task<IResult> (Guid id, GetAttachmentHandler handler, HttpContext http, CancellationToken ct) =>
            {
                var result = await handler.Content(id, ct);
                if (result.IsFailure) return result.Error!.ToProblem();

                var file = result.Value;
                var disposition = new ContentDispositionHeaderValue(file.IsImage ? "inline" : "attachment");
                disposition.SetHttpFileName(file.FileName);
                http.Response.Headers.ContentDisposition = disposition.ToString();
                http.Response.Headers.CacheControl = "private, max-age=3600";
                return TypedResults.Stream(file.Content, file.ContentType);
            })
            .RequireAuthorization(AuthPolicies.OrganizationMember);
    }
}
