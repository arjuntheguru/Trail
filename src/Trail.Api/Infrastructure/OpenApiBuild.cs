using System.Reflection;

namespace Trail.Api.Infrastructure;

/// <summary>
/// Support for generating the OpenAPI document at build time (Microsoft.Extensions.ApiDescription.Server). The
/// generator starts the host only to read the document, so it gets inert settings: nothing connects anywhere.
/// </summary>
internal static class OpenApiBuild
{
    public static bool IsRunning => Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

    public static readonly IReadOnlyDictionary<string, string?> Settings = new Dictionary<string, string?>
    {
        ["Database:ConnectionString"] = "Server=openapi-build.invalid;Database=none",
        ["Database:MigrateOnStartup"] = "false",
        ["Jwt:SigningKey"] = "openapi-build-placeholder-signing-key-not-a-secret",
        ["Bootstrap:PlatformAdminEmail"] = null,
        ["TaskGeneration:Enabled"] = "false",
        ["NotificationEmail:Enabled"] = "false",
        ["IssueEscalation:Enabled"] = "false",
        ["DailyDigest:Enabled"] = "false",
        ["Webhooks:Enabled"] = "false",
        ["Storage:Provider"] = "Local",
    };
}