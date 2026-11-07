using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using NSubstitute;

using Serilog;
using Serilog.Events;

using Trail.Api.Infrastructure;
using Trail.Application.Common.Security;

namespace Trail.UnitTests.Common;

public sealed class HttpEdgeTests
{
    private static DefaultHttpContext NewContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var feature = new CallbackResponseFeature();
        context.Features.Set<IHttpResponseFeature>(feature);
        return context;
    }

    /// <summary>DefaultHttpContext never fires OnStarting callbacks; this feature fires them on <see cref="StartAsync"/>.</summary>
    private sealed class CallbackResponseFeature : IHttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _callbacks = [];

        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = new MemoryStream();
        public bool HasStarted { get; private set; }

        public void OnStarting(Func<object, Task> callback, object state) => _callbacks.Add((callback, state));

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }

        public async Task FireAsync()
        {
            HasStarted = true;
            foreach (var (callback, state) in _callbacks) await callback(state);
        }
    }

    // Runs the middleware with a terminal handler that starts the response (as an endpoint would).
    private static async Task<HttpContext> RunSecurityHeadersAsync(HttpContext context, Action<HttpContext>? endpoint = null)
    {
        var middleware = new SecurityHeadersMiddleware(async ctx =>
        {
            endpoint?.Invoke(ctx);
            // like the server does when the first byte is written
            await ((CallbackResponseFeature)ctx.Features.Get<IHttpResponseFeature>()!).FireAsync();
        });
        await middleware.InvokeAsync(context);
        return context;
    }

    [Fact]
    public async Task Security_headers_include_permissions_policy_and_no_store()
    {
        var context = await RunSecurityHeadersAsync(NewContext());

        var headers = context.Response.Headers;
        headers.XContentTypeOptions.ToString().ShouldBe("nosniff");
        headers["Permissions-Policy"].ToString().ShouldContain("camera=()");
        headers["Permissions-Policy"].ToString().ShouldContain("microphone=()");
        headers["Permissions-Policy"].ToString().ShouldContain("geolocation=()");
        headers.CacheControl.ToString().ShouldBe("no-store");
        headers.ContentSecurityPolicy.ToString().ShouldContain("default-src 'none'");
    }

    [Fact]
    public async Task Security_headers_keep_cache_control_set_by_the_endpoint()
    {
        var context = await RunSecurityHeadersAsync(NewContext(), ctx => ctx.Response.Headers.CacheControl = "private, max-age=3600");

        context.Response.Headers.CacheControl.ToString().ShouldBe("private, max-age=3600");
    }

    [Fact]
    public async Task Security_headers_skip_csp_for_the_docs_ui()
    {
        var context = NewContext();
        context.Request.Path = "/scalar/v1";

        await RunSecurityHeadersAsync(context);

        context.Response.Headers.ContentSecurityPolicy.ToString().ShouldBeEmpty();
    }

    private static async Task<string> RunRequestIdAsync(HttpContext context)
    {
        await new RequestIdMiddleware(_ => Task.CompletedTask).InvokeAsync(context);
        return context.Response.Headers[RequestIdMiddleware.HeaderName].ToString();
    }

    [Fact]
    public async Task Request_id_is_echoed_when_safe()
    {
        var context = NewContext();
        context.Request.Headers[RequestIdMiddleware.HeaderName] = "abc-123_X.y:z";

        (await RunRequestIdAsync(context)).ShouldBe("abc-123_X.y:z");
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("inject\nheader")]
    [InlineData("<script>")]
    public async Task Request_id_falls_back_to_trace_identifier_when_unsafe_or_missing(string supplied)
    {
        var context = NewContext();
        context.TraceIdentifier = "trace-1";
        if (supplied.Length > 0) context.Request.Headers[RequestIdMiddleware.HeaderName] = supplied;

        (await RunRequestIdAsync(context)).ShouldBe("trace-1");
    }

    [Fact]
    public async Task Request_id_longer_than_64_characters_is_replaced()
    {
        var context = NewContext();
        context.TraceIdentifier = "trace-2";
        context.Request.Headers[RequestIdMiddleware.HeaderName] = new string('a', 65);

        (await RunRequestIdAsync(context)).ShouldBe("trace-2");

        RequestIdMiddleware.IsAcceptable(new string('a', 64)).ShouldBeTrue();
    }

    [Fact]
    public async Task Health_writer_emits_status_and_durations_without_exception_details()
    {
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["database"] = new(HealthStatus.Unhealthy, "db down", TimeSpan.FromMilliseconds(12),
                    new InvalidOperationException("connection string secret"), null),
            },
            TimeSpan.FromMilliseconds(15));
        var context = NewContext();

        await HealthResponseWriter.WriteAsync(context, report);

        context.Response.ContentType.ShouldBe("application/json");
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        body.ShouldNotContain("secret");
        body.ShouldNotContain("db down");
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("status").GetString().ShouldBe("Unhealthy");
        var check = json.RootElement.GetProperty("checks").EnumerateArray().Single();
        check.GetProperty("name").GetString().ShouldBe("database");
        check.GetProperty("status").GetString().ShouldBe("Unhealthy");
        check.GetProperty("durationMs").GetInt64().ShouldBe(12);
    }

    [Fact]
    public async Task Rate_limit_rejection_writes_problem_details_with_retry_after()
    {
        var context = NewContext();

        await RateLimitingSetup.RejectAsync(new OnRejectedContext { HttpContext = context, Lease = new FixedLease(TimeSpan.FromSeconds(42)) },
            TestContext.Current.CancellationToken);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status429TooManyRequests);
        context.Response.Headers.RetryAfter.ToString().ShouldBe("42");
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        JsonDocument.Parse(body).RootElement.GetProperty("status").GetInt32().ShouldBe(429);
    }

    [Fact]
    public async Task Rate_limit_rejection_without_retry_after_omits_the_header()
    {
        var context = NewContext();

        await RateLimitingSetup.RejectAsync(new OnRejectedContext { HttpContext = context, Lease = new FixedLease(null) },
            TestContext.Current.CancellationToken);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status429TooManyRequests);
        context.Response.Headers.ContainsKey("Retry-After").ShouldBeFalse();
    }

    private sealed class FixedLease(TimeSpan? retryAfter) : RateLimitLease
    {
        public override bool IsAcquired => false;
        public override IEnumerable<string> MetadataNames => retryAfter is null ? [] : [MetadataName.RetryAfter.Name];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = retryAfter;
            return retryAfter is not null && metadataName == MetadataName.RetryAfter.Name;
        }
    }

    [Theory]
    [InlineData("/api/v1/tags", 200, LogEventLevel.Information)]
    [InlineData("/api/v1/tags", 404, LogEventLevel.Information)]
    [InlineData("/api/v1/tags", 500, LogEventLevel.Error)]
    [InlineData("/health/live", 200, LogEventLevel.Debug)]
    [InlineData("/health/ready", 503, LogEventLevel.Error)] // a failing probe is still worth seeing
    public void Request_log_level_depends_on_status_and_path(string path, int status, LogEventLevel expected)
    {
        var context = NewContext();
        context.Request.Path = path;
        context.Response.StatusCode = status;

        RequestLogging.GetLevel(context, 1, null).ShouldBe(expected);
    }

    [Fact]
    public void Request_log_level_is_error_for_exceptions()
    {
        var context = NewContext();
        context.Request.Path = "/health/ready";

        RequestLogging.GetLevel(context, 1, new InvalidOperationException()).ShouldBe(LogEventLevel.Error);
    }

    [Fact]
    public void Request_logging_enriches_with_user_org_and_client_ip()
    {
        var context = NewContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.9");
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(TrailClaimTypes.UserId, "u-1"), new Claim(TrailClaimTypes.OrganizationId, "o-1")], "test"));
        var diagnostics = Substitute.For<IDiagnosticContext>();

        RequestLogging.Enrich(diagnostics, context);

        diagnostics.Received().Set("ClientIp", "203.0.113.9", false);
        diagnostics.Received().Set("UserId", "u-1", false);
        diagnostics.Received().Set("OrganizationId", "o-1", false);
    }

    [Fact]
    public void Request_logging_skips_identity_for_anonymous_requests()
    {
        var diagnostics = Substitute.For<IDiagnosticContext>();

        RequestLogging.Enrich(diagnostics, NewContext());

        diagnostics.DidNotReceive().Set("UserId", Arg.Any<object?>(), Arg.Any<bool>());
        diagnostics.DidNotReceive().Set("OrganizationId", Arg.Any<object?>(), Arg.Any<bool>());
    }
}