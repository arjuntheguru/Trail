using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Files;

namespace Trail.Application.Features.Attachments;

/// <summary>An uploaded file. <see cref="ClaimedFileName"/> is untrusted; the real type is read from the content.</summary>
public sealed record UploadAttachmentCommand(Stream Content, string? ClaimedFileName, AttachmentKind Kind, Guid SiteId);

public sealed class UploadAttachmentHandler(
    IAppDbContext db, ISiteAccess access, IFileStorage storage, ITenantContext tenant, TimeProvider clock)
{
    private const int HeaderLength = 32;

    public async Task<Result<AttachmentResponse>> Handle(UploadAttachmentCommand command, CancellationToken ct)
    {
        if (tenant.OrganizationId is not { } organizationId)
            return Error.Forbidden("attachment.no_organization", "Choose an organization first.");

        // A site outside the caller's scope looks like a site that does not exist.
        if (!await access.CanAccessSiteAsync(command.SiteId, ct)
            || !await db.Sites.AnyAsync(s => s.Id == command.SiteId, ct))
            return Error.NotFound("site.not_found", "Site not found.");

        var content = command.Content;
        if (!content.CanSeek)
        {
            var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct);
            content = buffer;
        }

        if (content.Length == 0)
            return Error.Validation("attachment.empty", "The file is empty.");
        if (content.Length > Attachment.MaxBytes)
            return Error.Validation("attachment.too_large", $"Files are limited to {Attachment.MaxBytes / (1024 * 1024)} MB.");

        content.Position = 0;
        var header = new byte[HeaderLength];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        var contentType = FileSignatures.Detect(header.AsSpan(0, read));

        var allowed = contentType is not null
                      && (contentType.StartsWith("image/", StringComparison.Ordinal)
                          || (command.Kind == AttachmentKind.Document && contentType == "application/pdf"));
        if (!allowed)
        {
            return Error.Validation("attachment.unsupported_type", command.Kind == AttachmentKind.Document
                ? "Upload a JPEG, PNG, WebP, HEIC image or a PDF."
                : "Upload a JPEG, PNG, WebP or HEIC image.");
        }

        content.Position = 0;
        var sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(content, ct));

        var now = clock.GetUtcNow().UtcDateTime;
        var extension = FileSignatures.Extension(contentType!);
        var attachment = new Attachment
        {
            Kind = command.Kind,
            FileName = SanitizeFileName(command.ClaimedFileName, extension),
            ContentType = contentType!,
            SizeBytes = content.Length,
            StorageKey = string.Empty,
            Sha256 = sha256,
            SiteId = command.SiteId,
        };
        attachment.StorageKey = $"{organizationId}/{now:yyyy}/{now:MM}/{attachment.Id}{extension}";

        content.Position = 0;
        await storage.SaveAsync(attachment.StorageKey, content, contentType!, ct);
        try
        {
            db.Attachments.Add(attachment);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Do not leave an orphaned file behind when the row could not be saved.
            await storage.DeleteAsync(attachment.StorageKey, CancellationToken.None);
            throw;
        }

        return new AttachmentResponse(attachment.Id, attachment.Kind, attachment.ContentType, attachment.SizeBytes, attachment.FileName, attachment.SiteId);
    }

    /// <summary>Drops any path and control characters from a client's file name and limits its length.</summary>
    public static string SanitizeFileName(string? claimed, string fallbackExtension)
    {
        var name = claimed ?? string.Empty;
        name = name[(name.LastIndexOfAny(['/', '\\']) + 1)..];
        name = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (name.Length == 0 || name.All(c => c == '.')) return $"upload{fallbackExtension}";

        if (name.Length <= Attachment.FileNameMaxLength) return name;

        var extension = Path.GetExtension(name);
        if (extension.Length is 0 or > 16) return name[..Attachment.FileNameMaxLength];
        return name[..(Attachment.FileNameMaxLength - extension.Length)] + extension;
    }
}
