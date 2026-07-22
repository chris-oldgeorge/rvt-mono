// File summary: Provides web-independent application result envelopes for business-layer workflows.
// Major updates:
// - 2026-07-05 pending Added transport-neutral result types for controller-to-business refactoring.
// - 2026-07-05 pending Added optional downstream status metadata for service-gateway failures.

namespace RVT.BusinessLogic.Application;

public enum ApplicationResultKind
{
    Success,
    NotFound,
    Forbidden,
    Validation,
    Conflict,
    ExternalServiceUnavailable
}

public sealed record ApplicationError(string Field, string Message);

public sealed class ApplicationResult<T>
{
    private ApplicationResult(ApplicationResultKind kind, T? value, IReadOnlyList<ApplicationError> errors, string? message, int? statusCode = null)
    {
        Kind = kind;
        Value = value;
        Errors = errors;
        Message = message;
        StatusCode = statusCode;
    }

    public ApplicationResultKind Kind { get; }
    public T? Value { get; }
    public IReadOnlyList<ApplicationError> Errors { get; }
    public string? Message { get; }
    public int? StatusCode { get; }

    public static ApplicationResult<T> Success(T value)
    {
        return new ApplicationResult<T>(ApplicationResultKind.Success, value, [], null);
    }

    public static ApplicationResult<T> NotFound(string message)
    {
        return new ApplicationResult<T>(ApplicationResultKind.NotFound, default, [], message);
    }

    public static ApplicationResult<T> Forbidden()
    {
        return new ApplicationResult<T>(ApplicationResultKind.Forbidden, default, [], null);
    }

    public static ApplicationResult<T> Validation(params ApplicationError[] errors)
    {
        return new ApplicationResult<T>(ApplicationResultKind.Validation, default, errors, null);
    }

    public static ApplicationResult<T> Conflict(string message)
    {
        return new ApplicationResult<T>(ApplicationResultKind.Conflict, default, [], message);
    }

    public static ApplicationResult<T> ExternalServiceUnavailable(string message, int? statusCode = null)
    {
        return new ApplicationResult<T>(ApplicationResultKind.ExternalServiceUnavailable, default, [], message, statusCode);
    }
}
