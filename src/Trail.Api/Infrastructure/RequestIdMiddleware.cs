using Serilog.Context;

namespace Trail.Api.Infrastructure;

/// <summary>
/// Correlates a request across systems: a caller-supplied X-Request-Id (short, safe characters only) is echoed back,
/// otherwise the trace identifier is. The value is added to every log event of the request as RequestId.
/// </summary>
internal sealed class RequestIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Request-Id";
    public const int MaxLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].ToString();
        var requestId = IsAcceptable(supplied) ? supplied : context.TraceIdentifier;

        context.Response.Headers[HeaderName] = requestId;
        using (LogContext.PushProperty("RequestId", requestId))
            await next(context);
    }

    internal static bool IsAcceptable(string? value) =>
        !string.IsNullOrEmpty(value) && value.Length <= MaxLength && value.All(IsSafe);

    private static bool IsSafe(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':';
}