using System.ComponentModel.DataAnnotations;
using System.Text;

using Microsoft.IdentityModel.Tokens;

using Trail.Application.Common.Settings;

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

    /// <summary>"StartTls" (port 587) or "SslOnConnect" (port 465). TLS is always enforced when credentials are set.</summary>
    public SmtpSecurity SmtpSecurity { get; set; } = SmtpSecurity.StartTls;

    [Range(1, 300)]
    public int SmtpTimeoutSeconds { get; set; } = 30;

    public bool HasCredentials => !string.IsNullOrEmpty(SmtpUser);

    public const string ProviderNotAllowedMessage =
        "Email:Provider must be 'Smtp' outside the Development and Testing environments (the Log sender writes account tokens to the logs).";

    /// <summary>The Log provider writes account tokens to the log, so it is only allowed in relaxed environments.</summary>
    public bool IsProviderAllowedIn(string environmentName) =>
        Provider == "Smtp" || EnvironmentRules.IsRelaxed(environmentName);
}

public enum SmtpSecurity
{
    StartTls,
    SslOnConnect,
}

public sealed class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    /// <summary>When set and no platform admin exists, a platform admin with this email is created at startup.</summary>
    [EmailAddress]
    public string? PlatformAdminEmail { get; set; }

    public string? PlatformAdminPassword { get; set; }

    public const int MinPasswordLength = 12;

    public const string InconsistentMessage =
        "Bootstrap:PlatformAdminPassword must be at least 12 characters when Bootstrap:PlatformAdminEmail is set.";

    /// <summary>When an admin email is configured the password must be present and long enough.</summary>
    public bool IsConsistent =>
        string.IsNullOrWhiteSpace(PlatformAdminEmail)
        || (PlatformAdminPassword is { } password && password.Length >= MinPasswordLength);
}