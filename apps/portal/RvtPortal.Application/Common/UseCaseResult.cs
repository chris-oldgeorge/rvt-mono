namespace RvtPortal.Application.Common;

public enum UseCaseResultKind
{
    Success,
    NotFound,
    Forbidden,
    Validation,
    Conflict,
    ExternalServiceUnavailable
}

public sealed record UseCaseError(string Field, string Message);

public sealed class UseCaseResult<T>
{
    private UseCaseResult(
        UseCaseResultKind kind,
        T? value,
        IReadOnlyList<UseCaseError> errors,
        string? message,
        int? statusCode)
    {
        Kind = kind;
        Value = value;
        Errors = errors;
        Message = message;
        StatusCode = statusCode;
    }

    public UseCaseResultKind Kind { get; }
    public T? Value { get; }
    public IReadOnlyList<UseCaseError> Errors { get; }
    public string? Message { get; }
    public int? StatusCode { get; }

    public static UseCaseResult<T> Success(T value) =>
        new(UseCaseResultKind.Success, value, [], null, null);

    public static UseCaseResult<T> NotFound(string message) =>
        new(UseCaseResultKind.NotFound, default, [], message, null);

    public static UseCaseResult<T> Forbidden() =>
        new(UseCaseResultKind.Forbidden, default, [], null, null);

    public static UseCaseResult<T> Validation(params UseCaseError[] errors) =>
        new(UseCaseResultKind.Validation, default, errors, null, null);

    public static UseCaseResult<T> Conflict(string message) =>
        new(UseCaseResultKind.Conflict, default, [], message, null);

    public static UseCaseResult<T> ExternalServiceUnavailable(
        string message,
        int? statusCode = null) =>
        new(UseCaseResultKind.ExternalServiceUnavailable, default, [], message, statusCode);
}
