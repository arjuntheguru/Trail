using Microsoft.Extensions.Options;
using Trail.Application.Common.Abstractions;
using Trail.Infrastructure.Files;

namespace Trail.UnitTests.Common;

public sealed class FileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"trail-storage-{Guid.NewGuid():N}");
    private readonly LocalFileStorage _storage;

    public FileStorageTests() => _storage = new LocalFileStorage(Options.Create(new StorageOptions { LocalPath = _root }));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Saves_and_reads_back_by_key()
    {
        await _storage.SaveAsync("org/2026/01/file.jpg", new MemoryStream([1, 2, 3]), "image/jpeg", Ct);

        await using var stream = await _storage.OpenReadAsync("org/2026/01/file.jpg", Ct);
        using var copy = new MemoryStream();
        await stream!.CopyToAsync(copy, Ct);
        copy.ToArray().ShouldBe([1, 2, 3]);
        (await _storage.OpenReadAsync("org/missing.jpg", Ct)).ShouldBeNull();
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("org/../../outside.txt")]
    [InlineData("C:/Windows/evil.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("")]
    public async Task Keys_cannot_escape_the_storage_root(string key)
    {
        await Should.ThrowAsync<ArgumentException>(() => _storage.SaveAsync(key, new MemoryStream([1]), "image/png", Ct));
    }

    [Fact]
    public void File_types_are_detected_from_content_not_names()
    {
        FileSignatures.Detect([0xFF, 0xD8, 0xFF, 0xE0, 0, 0]).ShouldBe("image/jpeg");
        FileSignatures.Detect([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0]).ShouldBe("image/png");
        FileSignatures.Detect("%PDF-1.7"u8).ShouldBe("application/pdf");
        FileSignatures.Detect("<html><script>"u8).ShouldBeNull();
        FileSignatures.Detect("MZ\x90\0"u8).ShouldBeNull();
    }
}
