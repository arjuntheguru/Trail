using Trail.Application.Common.Results;

namespace Trail.Api.Infrastructure;

public static class ResultExtensions
{
    public static IResult ToProblem(this Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status422UnprocessableEntity,
        };

        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };

        return error is { Type: ErrorType.Validation, Details: not null }
            ? TypedResults.ValidationProblem(error.Details, detail: error.Description, extensions: extensions)
            : TypedResults.Problem(detail: error.Description, statusCode: status, title: error.Code, extensions: extensions);
    }

    public static IResult ToHttp<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : result.Error!.ToProblem();

    public static IResult ToHttp<T>(this Result<T> result) => result.ToHttp(value => TypedResults.Ok(value));

    public static IResult ToHttp(this Result result) =>
        result.IsSuccess ? TypedResults.NoContent() : result.Error!.ToProblem();
}
