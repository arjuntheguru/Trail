namespace Trail.Infrastructure.Files;

internal static class StorageKeys
{
    /// <summary>
    /// Keys are server-generated "org/yyyy/MM/id.ext". Empty keys, parent segments, rooted paths, drive letters and
    /// backslashes are refused on every OS (on Linux a key like "C:/x" is not rooted, so the rooted check alone would accept it).
    /// </summary>
    public static void Validate(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(key)
            || key.IndexOfAny([':', '\\']) >= 0)
            throw new ArgumentException("Invalid storage key.", nameof(key));
    }
}