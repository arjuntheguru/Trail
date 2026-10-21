namespace Trail.Api.Infrastructure;

/// <summary>Conservative headers for a JSON API. The dev-only docs UI is exempt from the CSP so it can load.</summary>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";

        if (!context.Request.Path.StartsWithSegments("/scalar") && !context.Request.Path.StartsWithSegments("/openapi"))
            headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

        return next(context);
    }
}
