using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Trail.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "trail-api";

    [Required]
    public string Audience { get; set; } = "trail-app";

    /// <summary>HMAC-SHA256 key, at least 32 characters. Supply through secrets, never appsettings.json.</summary>
    [Required, MinLength(32)]
    public string SigningKey { get; set; } = "";

    [Range(1, 60)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 30;

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}

public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Public URL of the frontend, used for links in emails.</summary>
    [Required, Url]
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";

    /// <summary>Whether anyone can sign up and create an organization, or only platform admins can.</summary>
    public bool AllowSelfSignup { get; set; } = true;
}

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>"Smtp" to send, "Log" to only write emails to the log (development).</summary>
    [Required]
    public string Provider { get; set; } = "Log";

    [Required, EmailAddress]
    public string FromAddress { get; set; } = "no-reply@trail.local";

    public string FromName { get; set; } = "Trail";

    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUser { get; set; }
    public string? SmtpPassword { get; set; }
}

public sealed class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    /// <summary>When set and no platform admin exists, a platform admin with this email is created at startup.</summary>
    [EmailAddress]
    public string? PlatformAdminEmail { get; set; }

    public string? PlatformAdminPassword { get; set; }
}
