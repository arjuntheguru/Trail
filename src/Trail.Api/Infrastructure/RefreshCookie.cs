using Microsoft.AspNetCore.Http.HttpResults;

using Trail.Application.Features.Auth;

namespace Trail.Api.Infrastructure;

/// <summary>
/// Browser transport for refresh tokens. A client that sends <c>X-Refresh-Token-Transport: cookie</c> gets the
/// refresh token as an httpOnly cookie instead of in the response body, and may omit it from /auth/refresh and
/// /auth/logout. Requiring the custom header forces a CORS preflight, so another site cannot replay the cookie.
/// Clients without the header (mobile, scripts) keep the token-in-body contract unchanged.
/// </summary>
internal static class RefreshCookie
{
    public const string Name = "trail_refresh";
    public const string TransportHeader = "X-Refresh-Token-Transport";
    public const string CookieTransport = "cookie";
    public const string CookiePath = "/api/v1/auth";
    private const int MaxTokenLength = 128;

    public static bool Requested(HttpRequest request) =>
        string.Equals(request.Headers[TransportHeader], CookieTransport, StringComparison.OrdinalIgnoreCase);

    /// <summary>The refresh token from the body, or from the cookie when the client opted into cookie transport.</summary>
    public static string? Read(HttpRequest request, string? fromBody)
    {
        if (!string.IsNullOrEmpty(fromBody)) return fromBody;
        if (!Requested(request)) return null;
        var cookie = request.Cookies[Name];
        return string.IsNullOrEmpty(cookie) || cookie.Length > MaxTokenLength ? null : cookie;
    }

    public static CookieOptions Options(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true, // browsers treat http://localhost as a secure context, so this also works in development
        SameSite = SameSiteMode.Strict,
        Path = CookiePath,
        Expires = expires,
        IsEssential = true,
    };

    /// <summary>Moves the refresh token of a successful session response into the cookie for cookie clients.</summary>
    public static async ValueTask<object?> Filter(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var result = await next(context);
        var http = context.HttpContext;
        if (!Requested(http.Request)) return result;

        return result switch
        {
            Created<AuthSessionResponse> created => TypedResults.Created(created.Location, ToCookie(http, created.Value!)),
            Ok<AuthSessionResponse> ok => TypedResults.Ok(ToCookie(http, ok.Value!)),
            _ => result,
        };
    }

    public static void Clear(HttpResponse response) =>
        response.Cookies.Delete(Name, new CookieOptions { Path = CookiePath, Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict });

    /// <summary>Sets the cookie and returns the session without the token, so page scripts never see it.</summary>
    internal static AuthSessionResponse ToCookie(HttpContext http, AuthSessionResponse session)
    {
        http.Response.Cookies.Append(Name, session.RefreshToken,
            Options(new DateTimeOffset(DateTime.SpecifyKind(session.RefreshTokenExpiresAt, DateTimeKind.Utc))));
        return session with { RefreshToken = "" };
    }
}

public static class RefreshCookieExtensions
{
    /// <summary>Lets browser clients receive this endpoint's session refresh token as an httpOnly cookie.</summary>
    public static RouteHandlerBuilder WithRefreshCookie(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter(RefreshCookie.Filter);
}

/// <summary>Body of /auth/refresh and /auth/logout; the token may be omitted by cookie-transport clients.</summary>
public sealed record RefreshTokenRequest(string? RefreshToken);