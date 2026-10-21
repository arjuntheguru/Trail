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
}
