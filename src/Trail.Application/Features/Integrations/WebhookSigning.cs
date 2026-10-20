using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Trail.Application.Features.Integrations;

/// <summary>
/// Signs deliveries so receivers can verify they come from Trail and were not replayed: the signature covers
/// the Unix timestamp header and the exact body, as <c>v1=hex(HMAC-SHA256(secret, "{timestamp}.{body}"))</c>.
/// </summary>
public static class WebhookSigning
{
    public const string EventHeader = "Trail-Event";
    public const string DeliveryHeader = "Trail-Delivery";
    public const string TimestampHeader = "Trail-Timestamp";
    public const string SignatureHeader = "Trail-Signature";

    public static string NewSecret() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Sign(string secret, long unixSeconds, string body)
    {
        var signed = Encoding.UTF8.GetBytes($"{unixSeconds.ToString(CultureInfo.InvariantCulture)}.{body}");
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signed);
        return "v1=" + Convert.ToHexStringLower(mac);
    }

    /// <summary>The POST for one delivery attempt with its identifying and signature headers.</summary>
    public static HttpRequestMessage CreateRequest(
        string url, string eventType, Guid deliveryId, string secret, string body, DateTimeOffset now)
    {
        var seconds = now.ToUnixTimeSeconds();
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation(EventHeader, eventType);
        request.Headers.TryAddWithoutValidation(DeliveryHeader, deliveryId.ToString());
        request.Headers.TryAddWithoutValidation(TimestampHeader, seconds.ToString(CultureInfo.InvariantCulture));
        request.Headers.TryAddWithoutValidation(SignatureHeader, Sign(secret, seconds, body));
        return request;
    }
}
