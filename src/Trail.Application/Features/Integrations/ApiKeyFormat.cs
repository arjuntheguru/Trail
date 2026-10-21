using System.Security.Cryptography;
using System.Text;
using Trail.Domain.Integrations;

namespace Trail.Application.Features.Integrations;

/// <summary>
/// Generates, parses and hashes API keys. A key is <c>trl_{prefix}_{secret}</c>: an 8 character identifier
/// followed by 32 random base62 characters (about 190 bits). Only the SHA-256 hash is stored, so the key cannot
/// be recovered from the database; high-entropy keys need no slow hash or salt, and a plain hash lets the key be
/// looked up by an indexed equality.
/// </summary>
public static class ApiKeyFormat
{
    public const string Marker = "trl_";
    public const int SecretLength = 32;

    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    /// <summary>The full key (show once), its list-safe prefix and the hash to store.</summary>
    public static (string Key, string Prefix, string Hash) Generate()
    {
        var prefix = RandomNumberGenerator.GetString(Alphabet, ApiKey.PrefixLength);
        var secret = RandomNumberGenerator.GetString(Alphabet, SecretLength);
        var key = $"{Marker}{prefix}_{secret}";
        return (key, prefix, Hash(key));
    }

    /// <summary>Lowercase hex SHA-256 of the whole key.</summary>
    public static string Hash(string key) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    /// <summary>True when the presented key has the right shape; cheap check before touching the database.</summary>
    public static bool TryParse(string? key, out string prefix)
    {
        prefix = "";
        if (key is null || key.Length != Marker.Length + ApiKey.PrefixLength + 1 + SecretLength) return false;
        if (!key.StartsWith(Marker, StringComparison.Ordinal) || key[Marker.Length + ApiKey.PrefixLength] != '_') return false;

        var body = key.AsSpan(Marker.Length);
        foreach (var c in body[..ApiKey.PrefixLength]) if (!Alphabet.Contains(c)) return false;
        foreach (var c in body[(ApiKey.PrefixLength + 1)..]) if (!Alphabet.Contains(c)) return false;

        prefix = key.Substring(Marker.Length, ApiKey.PrefixLength);
        return true;
    }

    /// <summary>Compares a presented key's hash with a stored one in constant time.</summary>
    public static bool Matches(string presentedKey, string storedHash)
    {
        var presented = Encoding.ASCII.GetBytes(Hash(presentedKey));
        var stored = Encoding.ASCII.GetBytes(storedHash);
        return CryptographicOperations.FixedTimeEquals(presented, stored);
    }

    /// <summary>The key as it may be shown after creation: the prefix only.</summary>
    public static string Display(string prefix) => $"{Marker}{prefix}_…";
}
