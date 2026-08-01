namespace Shora.Application.Common.Results;

public enum ErrorKind
{
    Validation = 400,
    Unauthorized = 401,
    Forbidden = 403,
    NotFound = 404,
    PayloadTooLarge = 413,
    Conflict = 409,
    Failure = 500
}

public sealed record Error(string Code, string Message, ErrorKind Kind = ErrorKind.Validation)
{
    public static Error Validation(string code, string message) => new(code, message, ErrorKind.Validation);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorKind.Unauthorized);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorKind.Forbidden);

    public static Error NotFound(string code, string message) => new(code, message, ErrorKind.NotFound);

    public static Error PayloadTooLarge(string code, string message) => new(code, message, ErrorKind.PayloadTooLarge);

    public static Error Conflict(string code, string message) => new(code, message, ErrorKind.Conflict);

    public static Error Failure(string code, string message) => new(code, message, ErrorKind.Failure);

    public int StatusCode => (int)Kind;
}

public class Result
{
    protected Result(bool isSuccess, Error? error)
    {
        if (isSuccess && error is not null)
        {
            throw new InvalidOperationException("A successful result cannot contain an error.");
        }

        if (!isSuccess && error is null)
        {
            throw new InvalidOperationException("A failed result must contain an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error? Error { get; }

    public static Result Success() => new(true, null);

    public static Result Failure(Error error) => new(false, error);

    public static implicit operator Result(Error error) => Failure(error);
}

public sealed class Result<T> : Result
{
    private Result(T value)
        : base(true, null)
    {
        Value = value;
    }

    private Result(Error error)
        : base(false, error)
    {
        Value = default;
    }

    public T? Value { get; }

    public static Result<T> Success(T value) => new(value);

    public static new Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
}
