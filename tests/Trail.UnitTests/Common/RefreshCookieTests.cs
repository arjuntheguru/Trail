using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

using NSubstitute;

using Trail.Api.Infrastructure;
using Trail.Application.Features.Auth;

namespace Trail.UnitTests.Common;

public sealed class RefreshCookieTests
{
    private static readonly DateTime Expires = new(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);

    private static AuthSessionResponse Session() => new(
        "access", Expires.AddDays(-20), "refresh-secret", Expires,
        new UserSummary(Guid.NewGuid(), "a@b.test", "A", "B", true, false), null, [], []);

    private static DefaultHttpContext Context(bool cookieTransport, string? cookie = null)
    {
        var context = new DefaultHttpContext();
        if (cookieTransport) context.Request.Headers[RefreshCookie.TransportHeader] = "cookie";
        if (cookie is not null) context.Request.Headers.Cookie = $"{RefreshCookie.Name}={cookie}";
        return context;
    }

    private static async Task<object?> RunFilter(HttpContext http, object? result)
    {
        var invocation = Substitute.For<EndpointFilterInvocationContext>();
        invocation.HttpContext.Returns(http);
        return await RefreshCookie.Filter(invocation, _ => ValueTask.FromResult(result));
    }

    [Fact]
    public void Body_token_wins_over_cookie()
    {
        RefreshCookie.Read(Context(true, "from-cookie").Request, "from-body").ShouldBe("from-body");
    }

    [Fact]
    public void Cookie_is_read_only_with_the_transport_header()
    {
        RefreshCookie.Read(Context(true, "from-cookie").Request, null).ShouldBe("from-cookie");
        // Without the custom header (which forces a CORS preflight) a cross-site request cannot use the cookie.
        RefreshCookie.Read(Context(false, "from-cookie").Request, null).ShouldBeNull();
    }

    [Fact]
    public void Oversized_cookie_is_ignored()
    {
        RefreshCookie.Read(Context(true, new string('x', 129)).Request, null).ShouldBeNull();
    }

    [Fact]
    public async Task Cookie_clients_get_the_token_as_httponly_cookie_and_not_in_the_body()
    {
        var http = Context(true);

        var result = await RunFilter(http, TypedResults.Ok(Session()));

        var ok = result.ShouldBeOfType<Ok<AuthSessionResponse>>();
        ok.Value!.RefreshToken.ShouldBeEmpty();
        ok.Value.AccessToken.ShouldBe("access");
        var setCookie = http.Response.Headers.SetCookie.ToString();
        setCookie.ShouldContain($"{RefreshCookie.Name}=refresh-secret");
        setCookie.ShouldContain("httponly", Case.Insensitive);
        setCookie.ShouldContain("secure", Case.Insensitive);
        setCookie.ShouldContain("samesite=strict", Case.Insensitive);
        setCookie.ShouldContain("path=/api/v1/auth", Case.Insensitive);
    }

    [Fact]
    public async Task Created_sessions_keep_their_location()
    {
        var http = Context(true);

        var result = await RunFilter(http, TypedResults.Created("/api/v1/me", Session()));

        var created = result.ShouldBeOfType<Created<AuthSessionResponse>>();
        created.Location.ShouldBe("/api/v1/me");
        created.Value!.RefreshToken.ShouldBeEmpty();
    }

    [Fact]
    public async Task Body_clients_are_unchanged()
    {
        var http = Context(false);
        var original = TypedResults.Ok(Session());

        var result = await RunFilter(http, original);

        result.ShouldBeSameAs(original);
        http.Response.Headers.SetCookie.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Failures_pass_through_without_a_cookie()
    {
        var http = Context(true);
        var problem = TypedResults.Problem(statusCode: 401);

        var result = await RunFilter(http, problem);

        result.ShouldBeSameAs(problem);
        http.Response.Headers.SetCookie.Count.ShouldBe(0);
    }
}