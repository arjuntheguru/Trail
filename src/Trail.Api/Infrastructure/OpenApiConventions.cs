using System.Text;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing.Patterns;

namespace Trail.Api.Infrastructure;

/// <summary>
/// Endpoints return <see cref="IResult"/>, so the success response is declared explicitly, next to the route:
/// <c>.Returns&lt;TagResponse&gt;()</c>, <c>.Creates&lt;TagResponse&gt;()</c>, <c>.ReturnsNoContent()</c>.
/// Error responses that follow from the route itself (401/403/404, operation ids) are added once for every endpoint
/// by <see cref="AddOpenApiConventions"/>.
/// </summary>
public static partial class OpenApiConventions
{
    private const string ProblemContentType = "application/problem+json";

    public static RouteHandlerBuilder Returns<T>(this RouteHandlerBuilder builder) =>
        builder.Produces<T>(StatusCodes.Status200OK);

    public static RouteHandlerBuilder Creates<T>(this RouteHandlerBuilder builder) =>
        builder.Produces<T>(StatusCodes.Status201Created);

    public static RouteHandlerBuilder Accepted<T>(this RouteHandlerBuilder builder) =>
        builder.Produces<T>(StatusCodes.Status202Accepted);

    public static RouteHandlerBuilder ReturnsNoContent(this RouteHandlerBuilder builder) =>
        builder.Produces(StatusCodes.Status204NoContent);

    /// <summary>The handler can refuse because of the current state of the data (<c>Error.Conflict</c>).</summary>
    public static RouteHandlerBuilder ReturnsConflict(this RouteHandlerBuilder builder) =>
        builder.ProducesProblem(StatusCodes.Status409Conflict);

    public static RouteHandlerBuilder ReturnsCsv(this RouteHandlerBuilder builder) =>
        builder.Produces<string>(StatusCodes.Status200OK, "text/csv");

    /// <summary>A binary body; its content type is the stored file's.</summary>
    public static RouteHandlerBuilder ReturnsFile(this RouteHandlerBuilder builder) =>
        builder.Produces<Stream>(StatusCodes.Status200OK, "application/octet-stream");

    /// <summary>
    /// For every endpoint below <paramref name="group"/>: 401/403 when it requires authorization, 404 when the route
    /// has a parameter (the id may not be visible to the caller), and an operation id derived from method and route.
    /// Runs after the endpoint's own conventions, so it sees the final metadata and never repeats a declared status.
    /// </summary>
    public static void AddOpenApiConventions(this RouteGroupBuilder group) =>
        ((IEndpointConventionBuilder)group).Finally(builder =>
        {
            if (builder is not RouteEndpointBuilder { RoutePattern: var pattern })
                return;

            var metadata = builder.Metadata;
            var anonymous = metadata.OfType<IAllowAnonymous>().Any();
            if (!anonymous && metadata.OfType<IAuthorizeData>().Any())
            {
                AddProblem(metadata, StatusCodes.Status401Unauthorized);
                AddProblem(metadata, StatusCodes.Status403Forbidden);
            }

            if (pattern.Parameters.Count > 0)
                AddProblem(metadata, StatusCodes.Status404NotFound);

            var method = metadata.OfType<IHttpMethodMetadata>().FirstOrDefault()?.HttpMethods.FirstOrDefault();
            if (method is not null && !metadata.OfType<IEndpointNameMetadata>().Any())
                metadata.Add(new EndpointNameMetadata(OperationId(method, pattern)));
        });

    private static void AddProblem(IList<object> metadata, int status)
    {
        if (metadata.OfType<IProducesResponseTypeMetadata>().Any(m => m.StatusCode == status))
            return;
        metadata.Add(new ProducesResponseTypeMetadata(status, typeof(ProblemDetails), [ProblemContentType]));
    }

    /// <summary>GET /api/v1/sites/{id}/tasks becomes <c>getSitesByIdTasks</c>.</summary>
    internal static string OperationId(string method, RoutePattern pattern)
    {
        var id = new StringBuilder(method.ToLowerInvariant());
        foreach (var segment in pattern.PathSegments)
        {
            if (segment.Parts.FirstOrDefault() is RoutePatternParameterPart parameter)
                id.Append("By").Append(Pascal(parameter.Name));
            else if (segment.Parts.FirstOrDefault() is RoutePatternLiteralPart literal && literal.Content is not "api" and not "v1")
                id.Append(Pascal(literal.Content));
        }

        return id.ToString();
    }

    private static string Pascal(string text) =>
        string.Concat(NonWord().Split(text).Where(word => word.Length > 0).Select(word => char.ToUpperInvariant(word[0]) + word[1..]));

    [GeneratedRegex("[^A-Za-z0-9]+")]
    private static partial Regex NonWord();
}