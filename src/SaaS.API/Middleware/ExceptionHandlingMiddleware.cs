using System.Net;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Exceptions;
using SaaS.Domain.Common;

namespace SaaS.API.Middleware;

/// <summary>
/// Translates domain, application, concurrency, and unexpected errors into sanitized RFC 9457 ProblemDetails.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, problem) = MapException(context, exception);

        if (statusCode >= 500)
        {
            _logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }
        else
        {
            _logger.LogWarning("Handled application exception ({StatusCode}) at {Method} {Path}: {Message}",
                statusCode, context.Request.Method, context.Request.Path, exception.Message);
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem, context.RequestAborted);
    }

    private (int StatusCode, ProblemDetails Problem) MapException(HttpContext context, Exception exception)
    {
        ProblemDetails CreateBase(int status, string title, string detail, string errorCode)
        {
            var pd = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = context.Request.Path,
                Type = $"https://httpstatuses.io/{status}"
            };

            pd.Extensions["errorCode"] = errorCode;
            pd.Extensions["correlationId"] = context.TraceIdentifier;
            return pd;
        }

        switch (exception)
        {
            case ValidationAppException appVal:
                var valPd = CreateBase(appVal.StatusCode, appVal.Title, appVal.Message, appVal.ErrorCode);
                valPd.Extensions["errors"] = appVal.Errors;
                return (appVal.StatusCode, valPd);

            case ValidationException fluentVal:
                var grouped = fluentVal.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                var fluentPd = CreateBase(
                    (int)HttpStatusCode.BadRequest,
                    "Validation Failed",
                    "One or more validation errors occurred.",
                    "validation_error");
                fluentPd.Extensions["errors"] = grouped;
                return ((int)HttpStatusCode.BadRequest, fluentPd);

            case AppException appEx:
                return (appEx.StatusCode, CreateBase(appEx.StatusCode, appEx.Title, appEx.Message, appEx.ErrorCode));

            case DomainException domainEx:
                return ((int)HttpStatusCode.UnprocessableEntity,
                    CreateBase((int)HttpStatusCode.UnprocessableEntity, "Business Rule Violation", domainEx.Message, domainEx.Code));

            case DbUpdateConcurrencyException:
                return ((int)HttpStatusCode.Conflict,
                    CreateBase((int)HttpStatusCode.Conflict, "Concurrency Conflict",
                        "The resource was modified by another request. Please refresh and try again.",
                        "concurrency_conflict"));

            default:
                var detail = _environment.IsDevelopment()
                    ? exception.Message
                    : "An unexpected internal error occurred.";
                return ((int)HttpStatusCode.InternalServerError,
                    CreateBase((int)HttpStatusCode.InternalServerError, "Internal Server Error", detail, "internal_error"));
        }
    }
}
