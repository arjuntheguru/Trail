namespace Trail.Api.Infrastructure;

/// <summary>Conservative headers for a JSON API. The dev-only docs UI is exempt from the CSP so it can load.</summary>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    internal const string PermissionsPolicy =
        "accelerometer=(), autoplay=(), camera=(), display-capture=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Permissions-Policy"] = PermissionsPolicy;

        if (!context.Request.Path.StartsWithSegments("/scalar") && !context.Request.Path.StartsWithSegments("/openapi"))
            headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

        // API responses carry per-tenant data: never let a shared cache keep them. Endpoints that choose their own
        // Cache-Control (attachment downloads, reports) keep it, so this is decided when the response starts.
        context.Response.OnStarting(static state =>
        {
            var response = (HttpResponse)state;
            if (!response.Headers.ContainsKey("Cache-Control")) response.Headers.CacheControl = "no-store";
            return Task.CompletedTask;
        }, context.Response);

        return next(context);
    }
}