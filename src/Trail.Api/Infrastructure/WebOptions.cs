namespace Trail.Api.Infrastructure;

public sealed class FrontendCorsOptions
{
    public const string SectionName = "Cors";
    public const string PolicyName = "Frontend";

    /// <summary>Exact origins allowed to call the API from a browser, e.g. "https://app.trail.example".</summary>
    public string[] AllowedOrigins { get; set; } = [];
}

public static class RateLimitPolicies
{
    /// <summary>Tight per-IP limit for login, refresh, password reset and similar unauthenticated endpoints.</summary>
    public const string Auth = "auth";
}
