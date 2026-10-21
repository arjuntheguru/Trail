using System.ComponentModel.DataAnnotations;

namespace Trail.Application.Features.Integrations;

public sealed class WebhookOptions
{
    public const string SectionName = "Webhooks";
    public const string HttpClientName = "webhooks";

    /// <summary>Whether this instance runs the delivery job.</summary>
    public bool Enabled { get; set; } = true;

    [Range(1, 3600)]
    public int IntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Development only: allows http and private or loopback targets so a local receiver can be used. Off by
    /// default; with it off, targets must be https and resolve to public addresses.
    /// </summary>
    public bool AllowInsecureTargets { get; set; }

    /// <summary>Whether <see cref="AllowInsecureTargets"/> may be on in the named hosting environment.</summary>
    public static bool InsecureTargetsAllowedIn(string environmentName) =>
        environmentName is "Development" or "Testing";
}
