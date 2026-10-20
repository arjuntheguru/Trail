using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Files;

namespace Trail.Application.Features.Attachments;

/// <summary>Reads attachment metadata and content. Files at sites outside the caller's scope look like missing files.</summary>
public sealed class GetAttachmentHandler(IAppDbContext db, ISiteAccess access, IFileStorage storage)
{
    private static readonly Error NotFound = Error.NotFound("attachment.not_found", "Attachment not found.");

    public async Task<Result<AttachmentResponse>> Handle(Guid id, CancellationToken ct)
    {
        var attachment = await FindAsync(id, ct);
        return attachment is null
            ? NotFound
            : new AttachmentResponse(
                attachment.Id, attachment.Kind, attachment.ContentType, attachment.SizeBytes, attachment.FileName, attachment.SiteId);
    }

    public async Task<Result<AttachmentContent>> Content(Guid id, CancellationToken ct)
    {
        var attachment = await FindAsync(id, ct);
        if (attachment is null) return NotFound;

        var stream = await storage.OpenReadAsync(attachment.StorageKey, ct);
        return stream is null
            ? Error.NotFound("attachment.content_missing", "The file is no longer available.")
            : new AttachmentContent(
                stream, attachment.ContentType, attachment.FileName, attachment.ContentType.StartsWith("image/", StringComparison.Ordinal));
    }

    private async Task<Attachment?> FindAsync(Guid id, CancellationToken ct)
    {
        var attachment = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
        if (attachment is null) return null;

        // Files without a site are organization-wide; the rest follow the caller's site scope.
        return attachment.SiteId is not { } siteId || await access.CanAccessSiteAsync(siteId, ct) ? attachment : null;
    }
}
