namespace Trail.Application.Common.Settings;

/// <summary>Which hosting environments may run with development conveniences (log emails, insecure webhooks, http URLs).</summary>
public static class EnvironmentRules
{
    public static bool IsRelaxed(string environmentName) => environmentName is "Development" or "Testing";
}