using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NSubstitute;
using Trail.Infrastructure.Files;

namespace Trail.UnitTests.Common;

public sealed class StorageOptionsTests
{
    private static StorageOptions S3(Action<S3StorageOptions>? configure = null)
    {
        var o = new StorageOptions { Provider = "S3", S3 = { Bucket = "trail" } };
        configure?.Invoke(o.S3);
        return o;
    }

    [Fact]
    public void Local_is_the_default_and_valid()
    {
        new StorageOptions().Validate("Production").ShouldBeNull();
        new StorageOptions { LocalPath = " " }.Validate("Production").ShouldNotBeNull();
    }

    [Fact]
    public void Unknown_provider_fails() => new StorageOptions { Provider = "Azure" }.Validate("Development").ShouldNotBeNull();

    [Fact]
    public void S3_requires_a_bucket()
    {
        S3().Validate("Production").ShouldBeNull();
        S3(s => s.Bucket = "").Validate("Production").ShouldNotBeNull();
    }

    [Theory]
    [InlineData("key", null)]
    [InlineData(null, "secret")]
    [InlineData("key", " ")]
    public void Access_key_and_secret_must_be_set_together(string? key, string? secret) =>
        S3(s => { s.AccessKeyId = key; s.SecretAccessKey = secret; }).Validate("Development").ShouldNotBeNull();

    [Fact]
    public void Both_or_neither_credentials_are_fine()
    {
        S3(s => { s.AccessKeyId = "k"; s.SecretAccessKey = "s"; }).Validate("Production").ShouldBeNull();
        S3().Validate("Production").ShouldBeNull();
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("/relative")]
    [InlineData("ftp://host")]
    public void Service_url_must_be_absolute_http_or_https(string url) =>
        S3(s => s.ServiceUrl = url).Validate("Development").ShouldNotBeNull();

    [Fact]
    public void Http_service_url_is_allowed_only_in_relaxed_environments_or_with_opt_in()
    {
        var http = (Action<S3StorageOptions>)(s => s.ServiceUrl = "http://minio:9000");
        S3(http).Validate("Development").ShouldBeNull();
        S3(http).Validate("Testing").ShouldBeNull();
        S3(http).Validate("Production").ShouldNotBeNull();
        S3(s => { http(s); s.AllowInsecureServiceUrl = true; }).Validate("Production").ShouldBeNull();
        S3(s => s.ServiceUrl = "https://acct.r2.cloudflarestorage.com").Validate("Production").ShouldBeNull();
    }

    [Fact]
    public void Key_prefix_is_normalised_and_cannot_traverse()
    {
        S3(s => s.KeyPrefix = "/attachments").S3.NormalizedPrefix.ShouldBe("attachments/");
        S3(s => s.KeyPrefix = "a/b/").S3.NormalizedPrefix.ShouldBe("a/b/");
        S3().S3.NormalizedPrefix.ShouldBe("");
        S3(s => s.KeyPrefix = "../x").Validate("Production").ShouldNotBeNull();
    }
}

public sealed class StorageKeysTests
{
    [Theory]
    [InlineData("../x")]
    [InlineData("a/../b")]
    [InlineData("/etc/passwd")]
    [InlineData("C:/x")]
    [InlineData("a\\b")]
    [InlineData("")]
    [InlineData("  ")]
    public void Unsafe_keys_are_rejected(string key) => Should.Throw<ArgumentException>(() => StorageKeys.Validate(key));

    [Fact]
    public void Generated_keys_are_accepted() => Should.NotThrow(() => StorageKeys.Validate("0190aa/2026/01/0190bb.jpg"));
}

public sealed class S3FileStorageTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly IAmazonS3 _s3 = Substitute.For<IAmazonS3>();

    private S3FileStorage Create(Action<S3StorageOptions>? configure = null)
    {
        var o = new StorageOptions { Provider = "S3", S3 = { Bucket = "trail", KeyPrefix = "attachments/" } };
        configure?.Invoke(o.S3);
        return new S3FileStorage(_s3, Options.Create(o));
    }

    [Fact]
    public async Task Save_puts_the_prefixed_key_with_content_type_and_sse_on_aws()
    {
        PutObjectRequest? sent = null;
        _s3.PutObjectAsync(Arg.Do<PutObjectRequest>(r => sent = r), Arg.Any<CancellationToken>()).Returns(new PutObjectResponse());

        await Create().SaveAsync("org/2026/01/f.png", new MemoryStream([1]), "image/png", Ct);

        sent.ShouldNotBeNull();
        sent.BucketName.ShouldBe("trail");
        sent.Key.ShouldBe("attachments/org/2026/01/f.png");
        sent.ContentType.ShouldBe("image/png");
        sent.ServerSideEncryptionMethod.ShouldBe(ServerSideEncryptionMethod.AES256);
    }

    [Fact]
    public async Task Save_skips_sse_for_custom_service_urls()
    {
        PutObjectRequest? sent = null;
        _s3.PutObjectAsync(Arg.Do<PutObjectRequest>(r => sent = r), Arg.Any<CancellationToken>()).Returns(new PutObjectResponse());

        await Create(s => s.ServiceUrl = "https://r2.example").SaveAsync("a/b.png", new MemoryStream([1]), "image/png", Ct);

        sent!.ServerSideEncryptionMethod.ShouldBeNull();
    }

    [Fact]
    public async Task Save_buffers_non_seekable_streams()
    {
        PutObjectRequest? sent = null;
        long length = -1;
        _s3.PutObjectAsync(Arg.Do<PutObjectRequest>(r => { sent = r; length = r.InputStream.Length; }), Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse());

        await Create().SaveAsync("a/b.bin", new NonSeekable([1, 2, 3]), "application/pdf", Ct);

        sent.ShouldNotBeNull();
        length.ShouldBe(3);
    }

    [Fact]
    public async Task OpenRead_returns_null_when_missing()
    {
        _s3.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns<GetObjectResponse>(_ => throw new AmazonS3Exception("missing") { StatusCode = HttpStatusCode.NotFound, ErrorCode = "NoSuchKey" });

        (await Create().OpenReadAsync("a/b.png", Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task OpenRead_returns_the_object_stream()
    {
        GetObjectRequest? sent = null;
        _s3.GetObjectAsync(Arg.Do<GetObjectRequest>(r => sent = r), Arg.Any<CancellationToken>())
            .Returns(new GetObjectResponse { ResponseStream = new MemoryStream([7, 8]) });

        await using var stream = await Create().OpenReadAsync("a/b.png", Ct);

        sent!.Key.ShouldBe("attachments/a/b.png");
        using var copy = new MemoryStream();
        await stream!.CopyToAsync(copy, Ct);
        copy.ToArray().ShouldBe([7, 8]);
    }

    [Fact]
    public async Task OpenRead_rethrows_other_s3_failures()
    {
        _s3.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns<GetObjectResponse>(_ => throw new AmazonS3Exception("denied") { StatusCode = HttpStatusCode.Forbidden });

        await Should.ThrowAsync<AmazonS3Exception>(() => Create().OpenReadAsync("a/b.png", Ct));
    }

    [Fact]
    public async Task Delete_removes_the_prefixed_key()
    {
        DeleteObjectRequest? sent = null;
        _s3.DeleteObjectAsync(Arg.Do<DeleteObjectRequest>(r => sent = r), Arg.Any<CancellationToken>()).Returns(new DeleteObjectResponse());

        await Create().DeleteAsync("a/b.png", Ct);

        sent!.BucketName.ShouldBe("trail");
        sent.Key.ShouldBe("attachments/a/b.png");
    }

    [Fact]
    public async Task Invalid_keys_throw_before_any_s3_call()
    {
        var storage = Create();
        await Should.ThrowAsync<ArgumentException>(() => storage.SaveAsync("../x", new MemoryStream([1]), "image/png", Ct));
        await Should.ThrowAsync<ArgumentException>(() => storage.OpenReadAsync("C:/x", Ct));
        await Should.ThrowAsync<ArgumentException>(() => storage.DeleteAsync("", Ct));
        _s3.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Health_check_is_generic_when_the_bucket_is_unreachable()
    {
        _s3.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
            .Returns<ListObjectsV2Response>(_ => throw new AmazonS3Exception("secret-bucket-detail"));

        var result = await new StorageHealthCheck(Create()).CheckHealthAsync(new HealthCheckContext(), Ct);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldNotContain("secret");
        result.Exception.ShouldBeNull();
    }

    [Fact]
    public async Task Health_check_is_healthy_when_the_bucket_lists()
    {
        _s3.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>()).Returns(new ListObjectsV2Response());

        (await new StorageHealthCheck(Create()).CheckHealthAsync(new HealthCheckContext(), Ct)).Status.ShouldBe(HealthStatus.Healthy);
    }

    private sealed class NonSeekable(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
    }
}

public sealed class LocalStorageHealthTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"trail-health-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Writable_directory_is_healthy_and_leaves_no_probe_file()
    {
        var storage = new LocalFileStorage(Options.Create(new StorageOptions { LocalPath = _root }));

        var result = await new StorageHealthCheck(storage).CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Healthy);
        Directory.GetFileSystemEntries(_root).ShouldBeEmpty();
    }
}

public sealed class S3ClientFactoryTests
{
    [Fact]
    public void Custom_endpoint_survives_a_configured_region()
    {
        var options = new Trail.Infrastructure.Files.S3StorageOptions
        {
            Bucket = "trail", ServiceUrl = "http://minio:9000", Region = "us-east-1", ForcePathStyle = true,
            AccessKeyId = "k", SecretAccessKey = "s",
        };

        using var client = (Amazon.S3.AmazonS3Client)Trail.Infrastructure.Files.S3ClientFactory.Create(
            options, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        client.Config.ServiceURL.ShouldStartWith("http://minio:9000");
        client.Config.AuthenticationRegion.ShouldBe("us-east-1");
        ((Amazon.S3.AmazonS3Config)client.Config).ForcePathStyle.ShouldBeTrue();
    }
}
