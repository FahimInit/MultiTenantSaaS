namespace SaaS.Application.Common.Exceptions;

/// <summary>
/// Base exception for structured application-level HTTP ProblemDetails responses.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(int statusCode, string errorCode, string title, string message)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        Title = title;
    }

    public int StatusCode { get; }
    public string ErrorCode { get; }
    public string Title { get; }
}

public sealed class UnauthorizedAppException : AppException
{
    public UnauthorizedAppException(string message = "Authentication is required.", string errorCode = "unauthorized")
        : base(401, errorCode, "Unauthorized", message)
    {
    }
}

public sealed class ForbiddenAppException : AppException
{
    public ForbiddenAppException(string message = "You do not have permission to perform this action.", string errorCode = "forbidden")
        : base(403, errorCode, "Forbidden", message)
    {
    }
}

public sealed class NotFoundAppException : AppException
{
    public NotFoundAppException(string message, string errorCode = "resource_not_found")
        : base(404, errorCode, "Resource Not Found", message)
    {
    }
}

public sealed class ConflictAppException : AppException
{
    public ConflictAppException(string message, string errorCode = "conflict")
        : base(409, errorCode, "Conflict", message)
    {
    }
}

public sealed class QuotaExceededAppException : AppException
{
    public QuotaExceededAppException(string message, string errorCode = "quota_exceeded")
        : base(403, errorCode, "Subscription Limit Reached", message)
    {
    }
}

public sealed class ValidationAppException : AppException
{
    public ValidationAppException(IReadOnlyDictionary<string, string[]> errors, string message = "One or more validation errors occurred.")
        : base(400, "validation_error", "Validation Failed", message)
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
