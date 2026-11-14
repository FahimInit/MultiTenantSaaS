using System.Text.RegularExpressions;
using Serilog.Context;

namespace SaaS.API.Middleware;

public sealed partial class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = !string.IsNullOrWhiteSpace(incoming) && SafeCorrelationPattern().IsMatch(incoming)
            ? incoming
            : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    [GeneratedRegex("^[a-zA-Z0-9_-]{8,64}$", RegexOptions.Compiled)]
    private static partial Regex SafeCorrelationPattern();
}
