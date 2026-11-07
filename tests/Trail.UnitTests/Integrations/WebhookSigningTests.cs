using System.Security.Cryptography;
using System.Text;
using Trail.Application.Features.Integrations;

namespace Trail.UnitTests.Integrations;

public sealed class WebhookSigningTests
{
    private const string Body = "{\"id\":\"1\",\"type\":\"issue.raised\"}";

    [Fact]
    public void Signature_is_v1_hex_hmac_sha256_of_timestamp_dot_body()
    {
        var expected = "v1=" + Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes("s3cret"), Encoding.UTF8.GetBytes("1700000000." + Body)));

        WebhookSigning.Sign("s3cret", 1700000000, Body).ShouldBe(expected);
    }

    [Fact]
    public void Signature_is_lowercase_hex_with_version_prefix()
    {
        var signature = WebhookSigning.Sign("key", 1, "x");

        signature.ShouldStartWith("v1=");
        signature.Length.ShouldBe(3 + 64);
        signature.ShouldMatch("^v1=[0-9a-f]{64}$");
    }

    [Fact]
    public void Signature_changes_with_secret_timestamp_and_body()
    {
        var baseline = WebhookSigning.Sign("a", 100, "body");
        WebhookSigning.Sign("b", 100, "body").ShouldNotBe(baseline);
        WebhookSigning.Sign("a", 101, "body").ShouldNotBe(baseline);
        WebhookSigning.Sign("a", 100, "body ").ShouldNotBe(baseline);
        WebhookSigning.Sign("a", 100, "body").ShouldBe(baseline);
    }

    [Fact]
    public async Task Request_carries_the_documented_headers_and_a_matching_signature()
    {
        var deliveryId = Guid.CreateVersion7();
        var at = DateTimeOffset.FromUnixTimeSeconds(1700000000);

        using var request = WebhookSigning.CreateRequest("https://hooks.example.com/x", "issue.raised", deliveryId, "s3cret", Body, at);

        request.Method.ShouldBe(HttpMethod.Post);
        request.RequestUri!.ToString().ShouldBe("https://hooks.example.com/x");
        request.Content!.Headers.ContentType!.MediaType.ShouldBe("application/json");
        (await request.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe(Body);
        request.Headers.GetValues("Trail-Event").ShouldBe(["issue.raised"]);
        request.Headers.GetValues("Trail-Delivery").ShouldBe([deliveryId.ToString()]);
        request.Headers.GetValues("Trail-Timestamp").ShouldBe(["1700000000"]);
        request.Headers.GetValues("Trail-Signature").ShouldBe([WebhookSigning.Sign("s3cret", 1700000000, Body)]);
    }

    [Fact]
    public void New_secrets_are_43_char_base64url_and_unique()
    {
        var a = WebhookSigning.NewSecret();
        var b = WebhookSigning.NewSecret();

        a.ShouldNotBe(b);
        a.Length.ShouldBe(43); // 32 bytes
        a.ShouldMatch("^[A-Za-z0-9_-]+$");
    }
}
