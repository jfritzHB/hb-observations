namespace FieldApp.Application.Common;

public enum ErrorKind
{
    Validation,

    /// <summary>Missing, or concealed because the caller has no visibility of it (docs/07-security-operations.md).</summary>
    NotFound,

    /// <summary>Visible to the caller, but the operation is not permitted.</summary>
    Forbidden,

    Conflict,
}

public sealed record AppError(ErrorKind Kind, string Title, IReadOnlyDictionary<string, string[]>? ValidationErrors = null)
{
    public static AppError NotFound(string title = "Resource not found.") => new(ErrorKind.NotFound, title);

    public static AppError Forbidden(string title = "You do not have permission to perform this operation.") =>
        new(ErrorKind.Forbidden, title);

    public static AppError Conflict(string title) => new(ErrorKind.Conflict, title);

    public static AppError Validation(string field, string message) =>
        new(ErrorKind.Validation, "One or more validation errors occurred.", new Dictionary<string, string[]> { [field] = [message] });
}

public sealed class Result<T>
{
    internal Result(T? value, AppError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public AppError? Error { get; }

    public bool IsSuccess => Error is null;

    public static implicit operator Result<T>(AppError error) => new(default, error);

    public static implicit operator Result<T>(T value) => new(value, null);
}

public static class Result
{
    public static Result<T> Success<T>(T value) => new(value, null);

    public static Result<T> Failure<T>(AppError error) => new(default, error);
}
