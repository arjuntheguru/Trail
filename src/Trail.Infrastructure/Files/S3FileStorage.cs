using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

using Microsoft.Extensions.Options;

using Trail.Application.Common.Abstractions;

namespace Trail.Infrastructure.Files;

/// <summary>S3-compatible storage (AWS S3, Cloudflare R2, MinIO, ...) so several API replicas share attachments.</summary>
internal sealed class S3FileStorage(IAmazonS3 s3, IOptions<StorageOptions> options) : IFileStorage, IStorageProbe
{
    private S3StorageOptions Settings => options.Value.S3;

    private string ObjectKey(string key)
    {
        StorageKeys.Validate(key);
        return Settings.NormalizedPrefix + key;
    }

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        var objectKey = ObjectKey(key);

        // The SDK needs a seekable stream to sign and retry the payload.
        MemoryStream? buffered = null;
        try
        {
            var body = content;
            if (!content.CanSeek)
            {
                buffered = new MemoryStream();
                await content.CopyToAsync(buffered, ct);
                buffered.Position = 0;
                body = buffered;
            }

            var request = new PutObjectRequest
            {
                BucketName = Settings.Bucket,
                Key = objectKey,
                InputStream = body,
                ContentType = contentType,
                AutoCloseStream = false,
            };
            // R2/MinIO may reject the header, so request SSE only against AWS itself.
            if (string.IsNullOrWhiteSpace(Settings.ServiceUrl)) request.ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256;
            // No If-None-Match: not every S3-compatible store supports it, and keys carry a fresh Guid v7.
            await s3.PutObjectAsync(request, ct);
        }
        finally
        {
            if (buffered is not null) await buffered.DisposeAsync();
        }
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken ct)
    {
        var objectKey = ObjectKey(key);
        try
        {
            var response = await s3.GetObjectAsync(new GetObjectRequest { BucketName = Settings.Bucket, Key = objectKey }, ct);
            return new ResponseStream(response);
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound || e.ErrorCode == "NoSuchKey")
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        var objectKey = ObjectKey(key);
        await s3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = Settings.Bucket, Key = objectKey }, ct);
    }

    public async Task<bool> ProbeAsync(CancellationToken ct)
    {
        try
        {
            // ListObjectsV2 works on every S3-compatible store; GetBucketLocation does not.
            await s3.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = Settings.Bucket,
                Prefix = Settings.NormalizedPrefix,
                MaxKeys = 1,
            }, ct);
            return true;
        }
        catch (Exception e) when (e is AmazonServiceException or HttpRequestException)
        {
            return false;
        }
    }

    /// <summary>Read-only pass-through over the S3 response stream that also disposes the response.</summary>
    private sealed class ResponseStream(GetObjectResponse response) : Stream
    {
        private readonly Stream _inner = response.ResponseStream;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => response.ContentLength;
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => _inner.Read(buffer);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => _inner.ReadAsync(buffer, ct);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => _inner.ReadAsync(buffer, offset, count, ct);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) response.Dispose();
            base.Dispose(disposing);
        }
    }
}