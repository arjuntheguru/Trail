namespace Trail.Api.Infrastructure;

public sealed class FrontendCorsOptions
{
    public const string SectionName = "Cors";
    public const string PolicyName = "Frontend";

    /// <summary>Exact origins allowed to call the API from a browser, e.g. "https://app.trail.example".</summary>
    public string[] AllowedOrigins { get; set; } = [];
}

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Requests per minute per client IP across the whole API.</summary>
    public int GlobalPermitsPerMinute { get; set; } = 600;

    /// <summary>Requests per minute per client IP to sign-in, refresh and password endpoints.</summary>
    public int AuthPermitsPerMinute { get; set; } = 10;

    /// <summary>Requests per minute per API key to the sensor endpoints (each request may carry up to 500 readings).</summary>
    public int SensorPermitsPerMinute { get; set; } = 120;
}

public static class RateLimitPolicies
{
    /// <summary>Tight per-IP limit for login, refresh, password reset and similar unauthenticated endpoints.</summary>
    public const string Auth = "auth";

    /// <summary>Per-API-key limit for sensor readings.</summary>
    public const string Sensors = "sensors";
}
