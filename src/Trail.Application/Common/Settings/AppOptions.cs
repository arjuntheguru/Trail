using System.ComponentModel.DataAnnotations;

namespace Trail.Application.Common.Settings;

public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Public URL of the frontend, used for links in emails.</summary>
    [Required, Url]
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";

    /// <summary>Whether anyone can sign up and create an organization, or only platform admins can.</summary>
    public bool AllowSelfSignup { get; set; } = true;

    public const string InvalidForEnvironmentMessage =
        "App:FrontendBaseUrl must be an absolute https URL that is not localhost outside the Development and Testing environments.";

    /// <summary>Outside relaxed environments the frontend URL must be a public https URL (links in emails carry tokens).</summary>
    public bool IsValidFor(string environmentName)
    {
        if (EnvironmentRules.IsRelaxed(environmentName)) return true;
        if (!Uri.TryCreate(FrontendBaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return false;
        return !uri.IsLoopback
            && !uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            && !uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
    }
}