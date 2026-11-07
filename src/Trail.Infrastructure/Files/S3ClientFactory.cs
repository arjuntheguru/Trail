using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Trail.Infrastructure.Files;

/// <summary>Reports the specific reason a Storage section is invalid (the options validator is the only place that knows the environment).</summary>
internal sealed class StorageOptionsValidator(IHostEnvironment env) : IValidateOptions<StorageOptions>
{
    public ValidateOptionsResult Validate(string? name, StorageOptions options) =>
        options.Validate(env.EnvironmentName) is { } error ? ValidateOptionsResult.Fail(error) : ValidateOptionsResult.Success;
}

internal static class S3ClientFactory
{
    public static IAmazonS3 Create(S3StorageOptions o, ILogger logger)
    {
        var config = new AmazonS3Config { ForcePathStyle = o.ForcePathStyle };
        if (!string.IsNullOrWhiteSpace(o.ServiceUrl))
        {
            // Setting RegionEndpoint would replace the custom endpoint, so with a ServiceUrl the region only signs.
            config.ServiceURL = o.ServiceUrl;
            if (!string.IsNullOrWhiteSpace(o.Region)) config.AuthenticationRegion = o.Region;
            // S3-compatible stores (R2, older MinIO) may reject the SDK's default flexible checksums.
            config.RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED;
            config.ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED;
            if (o.ServiceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                logger.LogWarning("Storage:S3:ServiceUrl uses plain http; attachments travel unencrypted to the store.");
        }
        else if (!string.IsNullOrWhiteSpace(o.Region))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(o.Region);
        }

        return !string.IsNullOrWhiteSpace(o.AccessKeyId)
            ? new AmazonS3Client(new BasicAWSCredentials(o.AccessKeyId, o.SecretAccessKey), config)
            : new AmazonS3Client(config);
    }
}
