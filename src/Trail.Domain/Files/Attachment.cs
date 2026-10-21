using Trail.Domain.Common;

namespace Trail.Domain.Files;

public enum AttachmentKind
{
    Photo,
    Signature,
    Document,
}

/// <summary>
/// An uploaded file (photo evidence, signature, document). Bytes live in file storage under
/// <see cref="StorageKey"/>; files are private and only served to members of the owning organization.
/// </summary>
public sealed class Attachment : TenantEntity
{
    public const long MaxBytes = 10 * 1024 * 1024;
    public const int FileNameMaxLength = 255;

    public AttachmentKind Kind { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public required string StorageKey { get; set; }

    /// <summary>Hex SHA-256 of the content, for integrity checks and audit evidence.</summary>
    public required string Sha256 { get; set; }

    /// <summary>Site the file was captured at, so site-scoped members can only see their sites' files.</summary>
    public Guid? SiteId { get; set; }
}
