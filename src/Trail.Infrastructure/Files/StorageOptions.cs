using Trail.Application.Common.Settings;

namespace Trail.Infrastructure.Files;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";
    public const string LocalProvider = "Local";
    public const string S3Provider = "S3";

    /// <summary><c>Local</c> (default, single server or shared volume) or <c>S3</c> (any S3-compatible store).</summary>
    public string Provider { get; set; } = LocalProvider;

    /// <summary>Directory for uploaded files. Keep it outside the web root; files are served only through the API.</summary>
    public string LocalPath { get; set; } = "App_Data/uploads";

    public S3StorageOptions S3 { get; set; } = new();

    public bool IsS3 => Provider == S3Provider;

    /// <summary>Returns an error message, or null when the options are usable in the given environment.</summary>
    public string? Validate(string environmentName)
    {
        if (Provider is not (LocalProvider or S3Provider)) return "Storage:Provider must be 'Local' or 'S3'.";
        if (!IsS3) return string.IsNullOrWhiteSpace(LocalPath) ? "Storage:LocalPath is required." : null;

        if (string.IsNullOrWhiteSpace(S3.Bucket)) return "Storage:S3:Bucket is required when Storage:Provider is 'S3'.";
        if (string.IsNullOrWhiteSpace(S3.AccessKeyId) != string.IsNullOrWhiteSpace(S3.SecretAccessKey))
            return "Storage:S3:AccessKeyId and Storage:S3:SecretAccessKey must be set together or both left empty.";
        if (S3.KeyPrefix is { } prefix && prefix.Contains("..", StringComparison.Ordinal))
            return "Storage:S3:KeyPrefix must not contain '..'.";

        if (!string.IsNullOrWhiteSpace(S3.ServiceUrl))
        {
            if (!Uri.TryCreate(S3.ServiceUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                return "Storage:S3:ServiceUrl must be an absolute http or https URL.";
            if (uri.Scheme == "http" && !S3.AllowInsecureServiceUrl && !EnvironmentRules.IsRelaxed(environmentName))
                return "Storage:S3:ServiceUrl must use https outside Development and Testing (or set Storage:S3:AllowInsecureServiceUrl).";
        }
        return null;
    }
}

public sealed class S3StorageOptions
{
    public string Bucket { get; set; } = "";
    public string? Region { get; set; }

    /// <summary>Endpoint of an S3-compatible store (Cloudflare R2, MinIO, ...). Leave empty for AWS.</summary>
    public string? ServiceUrl { get; set; }

    public bool ForcePathStyle { get; set; }

    /// <summary>Optional static credentials; when empty the SDK's default credential chain applies (IAM role, env).</summary>
    public string? AccessKeyId { get; set; }

    public string? SecretAccessKey { get; set; }

    /// <summary>Optional key prefix inside the bucket, e.g. <c>attachments/</c>.</summary>
    public string? KeyPrefix { get; set; }

    /// <summary>Allows a plain-http ServiceUrl outside Development/Testing (e.g. an in-cluster MinIO). Logged as a warning.</summary>
    public bool AllowInsecureServiceUrl { get; set; }

    /// <summary>The prefix normalised to either empty or "a/b/".</summary>
    public string NormalizedPrefix
    {
        get
        {
            var p = (KeyPrefix ?? "").Trim().Trim('/');
            return p.Length == 0 ? "" : p + "/";
        }
    }
}
