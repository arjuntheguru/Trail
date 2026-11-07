using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace Trail.Api.Infrastructure;

public sealed class FrontendCorsOptions
{
    public const string SectionName = "Cors";
    public const string PolicyName = "Frontend";

    /// <summary>Exact origins allowed to call the API from a browser, e.g. "https://app.trail.example" (no path).</summary>
    public string[] AllowedOrigins { get; set; } = [];

    /// <summary>True for an absolute http(s) origin: scheme, host and optional port only.</summary>
    public static bool IsValidOrigin(string? origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https"
        && string.Equals(origin, uri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase)
        && string.IsNullOrEmpty(uri.UserInfo);
}

internal sealed class FrontendCorsOptionsValidator : IValidateOptions<FrontendCorsOptions>
{
    public ValidateOptionsResult Validate(string? name, FrontendCorsOptions options)
    {
        var invalid = options.AllowedOrigins.Where(o => !FrontendCorsOptions.IsValidOrigin(o)).ToList();
        return invalid.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"Cors:AllowedOrigins must be absolute http(s) origins without a path or trailing slash; invalid: {string.Join(", ", invalid)}.");
    }
}

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Requests per minute per client IP across the whole API.</summary>
    [Range(1, int.MaxValue)]
    public int GlobalPermitsPerMinute { get; set; } = 600;

    /// <summary>Requests per minute per client IP to sign-in, refresh and password endpoints.</summary>
    [Range(1, int.MaxValue)]
    public int AuthPermitsPerMinute { get; set; } = 10;

    /// <summary>Requests per minute per API key to the sensor endpoints (each request may carry up to 500 readings).</summary>
    [Range(1, int.MaxValue)]
    public int SensorPermitsPerMinute { get; set; } = 120;
}

public static class RateLimitPolicies
{
    /// <summary>Tight per-IP limit for login, refresh, password reset and similar unauthenticated endpoints.</summary>
    public const string Auth = "auth";

    /// <summary>Per-API-key limit for sensor readings.</summary>
    public const string Sensors = "sensors";
}

/// <summary>HTTP edge settings: proxy trust, HTTPS redirection and the request body cap.</summary>
public sealed class HttpOptions
{
    public const string SectionName = "Http";

    /// <summary>Redirect http to https outside Development. Turn off when TLS terminates at a proxy or container ingress.</summary>
    public bool HttpsRedirection { get; set; } = true;

    /// <summary>IP addresses of reverse proxies whose X-Forwarded-* headers are trusted. Empty keeps the framework default (loopback).</summary>
    public string[] KnownProxies { get; set; } = [];

    /// <summary>CIDR networks (e.g. "10.0.0.0/8") of proxies whose X-Forwarded-* headers are trusted.</summary>
    public string[] KnownNetworks { get; set; } = [];

    /// <summary>How many proxies in the chain to trust.</summary>
    [Range(1, 10)]
    public int ForwardLimit { get; set; } = 1;

    /// <summary>Global request body cap; endpoints such as attachment upload set their own larger limit.</summary>
    [Range(1, int.MaxValue)]
    public long MaxRequestBodyBytes { get; set; } = 1024 * 1024;

    public IEnumerable<string> Errors()
    {
        foreach (var proxy in KnownProxies)
            if (!IPAddress.TryParse(proxy, out _))
                yield return $"Http:KnownProxies entry '{proxy}' is not an IP address.";

        foreach (var network in KnownNetworks)
            if (!System.Net.IPNetwork.TryParse(network, out _))
                yield return $"Http:KnownNetworks entry '{network}' is not a CIDR network such as 10.0.0.0/8.";
    }

    /// <summary>Applies the proxy trust settings. With nothing configured the framework defaults stay in place.</summary>
    public void ApplyTo(ForwardedHeadersOptions options)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = ForwardLimit;
        if (KnownProxies.Length == 0 && KnownNetworks.Length == 0) return;

        // An explicit list replaces the loopback defaults: only the listed proxies are trusted.
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        foreach (var proxy in KnownProxies) options.KnownProxies.Add(IPAddress.Parse(proxy));
        foreach (var network in KnownNetworks) options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
    }
}

internal sealed class HttpOptionsValidator : IValidateOptions<HttpOptions>
{
    public ValidateOptionsResult Validate(string? name, HttpOptions options)
    {
        var errors = options.Errors().ToList();
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
