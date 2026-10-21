using Trail.Domain.Files;

namespace Trail.Application.Features.Attachments;

public sealed record AttachmentResponse(Guid Id, AttachmentKind Kind, string ContentType, long Size, string FileName, Guid? SiteId);

/// <summary>A file ready to stream. The caller owns <see cref="Content"/> and must dispose it.</summary>
public sealed record AttachmentContent(Stream Content, string ContentType, string FileName, bool IsImage);
