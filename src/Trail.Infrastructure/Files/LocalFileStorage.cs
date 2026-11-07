using Microsoft.Extensions.Options;
using Trail.Application.Common.Abstractions;

namespace Trail.Infrastructure.Files;

/// <summary>Disk-backed storage for single-server deployments (or a shared volume). Use S3 to scale out without one.</summary>
internal sealed class LocalFileStorage(IOptions<StorageOptions> options) : IFileStorage, IStorageProbe
{
    private string Root => Path.GetFullPath(options.Value.LocalPath);

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, ct);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct)
    {
        var path = Resolve(key);
        return Task.FromResult<Stream?>(File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null);
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        var path = Resolve(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public async Task<bool> ProbeAsync(CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(Root);
            var probe = Path.Combine(Root, $".health-{Guid.NewGuid():N}");
            await using (var file = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 16, useAsync: true))
                await file.WriteAsync(new byte[1], ct);
            File.Delete(probe);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Maps a key to a path inside the root, refusing anything that would escape it.</summary>
    private string Resolve(string key)
    {
        StorageKeys.Validate(key);
        var path = Path.GetFullPath(Path.Combine(Root, key));
        if (!path.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Invalid storage key.", nameof(key));
        return path;
    }
}
