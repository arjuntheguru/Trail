namespace Trail.Application.Common.Results;

public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
}

/// <summary>An expected failure. Codes are stable, dotted identifiers ("site.not_found") clients can switch on.</summary>
public sealed record Error(string Code, string Description, ErrorType Type)
{
    public IReadOnlyDictionary<string, string[]>? Details { get; init; }

    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);
    public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);
    public static Error Forbidden(string code, string description) => new(code, description, ErrorType.Forbidden);
    public static Error Unauthorized(string code, string description) => new(code, description, ErrorType.Unauthorized);
    public static Error Failure(string code, string description) => new(code, description, ErrorType.Failure);

    public static Error Validation(string code, string description, IReadOnlyDictionary<string, string[]>? details = null) =>
        new(code, description, ErrorType.Validation) { Details = details };
}

/// <summary>Outcome of an application operation. Expected failures travel as <see cref="Error"/>; exceptions are for bugs.</summary>
public class Result
{
    protected Result(Error? error) => Error = error;

    public Error? Error { get; }
    public bool IsSuccess => Error is null;
    public bool IsFailure => !IsSuccess;

    public static Result Success() => new(null);
    public static Result Failure(Error error) => new(error);
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    public static implicit operator Result(Error error) => Failure(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T? value, Error? error) : base(error) => _value = value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot read the value of a failed result ({Error!.Code}).");

    public static Result<T> Success(T value) => new(value, null);
    public static new Result<T> Failure(Error error) => new(default, error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
}
