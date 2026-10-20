using System.Security.Cryptography;
using System.Text;

namespace Trail.Application.Common.Security;

/// <summary>
/// Opaque bearer secrets (refresh tokens, emailed links). 256 bits of randomness, URL-safe; only the SHA-256
/// hash is persisted, so a database leak does not leak usable tokens.
/// </summary>
public static class SecretToken
{
    public static (string Token, string Hash) Create()
    {
        var token = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        return (token, Hash(token));
    }

    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
