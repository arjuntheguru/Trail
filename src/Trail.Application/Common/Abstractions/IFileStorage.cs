namespace Trail.Application.Common.Abstractions;

/// <summary>Private blob storage for attachments. Keys are opaque; callers never build file paths themselves.</summary>
public interface IFileStorage
{
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct);

    /// <summary>Returns null when the object does not exist.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct);

    Task DeleteAsync(string key, CancellationToken ct);
}

/// <summary>Identifies a file's real type from its first bytes, ignoring the client's claimed content type.</summary>
public static class FileSignatures
{
    public static string? Detect(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith((byte[])[0xFF, 0xD8, 0xFF])) return "image/jpeg";
        if (header.StartsWith((byte[])[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])) return "image/png";
        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8)) return "image/webp";
        if (header.Length >= 12 && header[4..8].SequenceEqual("ftyp"u8)
            && (header[8..12].SequenceEqual("heic"u8) || header[8..12].SequenceEqual("heix"u8) || header[8..12].SequenceEqual("mif1"u8)))
            return "image/heic";
        if (header.StartsWith("%PDF-"u8)) return "application/pdf";
        return null;
    }

    public static string Extension(string contentType) => contentType switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        "image/heic" => ".heic",
        "application/pdf" => ".pdf",
        _ => ".bin",
    };
}
