using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace SaaS.API.Extensions;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int AuthPermitLimit { get; init; } = 20;
    public int AuthWindowSeconds { get; init; } = 60;
    public int ApiPermitLimit { get; init; } = 120;
    public int ApiWindowSeconds { get; init; } = 60;
}

public static class RateLimitingExtensions
{
    public const string AuthPolicyName = "auth-fixed-window";
    public const string ApiPolicyName = "api-sliding-window";

    public static IServiceCollection AddPlatformRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>()
            ?? new RateLimitingOptions();

        var authPermit = Math.Max(1, settings.AuthPermitLimit);
        var authWindow = TimeSpan.FromSeconds(Math.Max(1, settings.AuthWindowSeconds));
        var apiPermit = Math.Max(1, settings.ApiPermitLimit);
        var apiWindow = TimeSpan.FromSeconds(Math.Max(1, settings.ApiWindowSeconds));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    var fallbackSeconds = context.HttpContext.Request.Path.StartsWithSegments("/api/v1/auth", StringComparison.OrdinalIgnoreCase)
                        ? Math.Max(1, settings.AuthWindowSeconds)
                        : Math.Max(1, (int)Math.Ceiling(settings.ApiWindowSeconds / 4.0));
                    context.HttpContext.Response.Headers.RetryAfter = fallbackSeconds.ToString(CultureInfo.InvariantCulture);
                }

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too Many Requests",
                    Detail = "Rate limit exceeded. Please retry after the cooldown period.",
                    Instance = context.HttpContext.Request.Path,
                    Type = "https://httpstatuses.io/429"
                };
                problem.Extensions["errorCode"] = "rate_limit_exceeded";
                problem.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(
                    problem,
                    options: null,
                    contentType: "application/problem+json",
                    cancellationToken: cancellationToken);
            };

            options.AddPolicy(AuthPolicyName, context =>
            {
                var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: $"auth:{remoteIp}",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = authPermit,
                        Window = authWindow,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });

            options.AddPolicy(ApiPolicyName, context =>
            {
                var tenantHeader = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
                var userId = context.User?.FindFirst("sub")?.Value;
                var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                // Tenant-scoped partitioning prevents one noisy tenant from starving unrelated tenants.
                var partitionKey = !string.IsNullOrWhiteSpace(tenantHeader)
                    ? $"tenant:{tenantHeader}:actor:{userId ?? remoteIp}"
                    : !string.IsNullOrWhiteSpace(userId)
                        ? $"user:{userId}"
                        : $"ip:{remoteIp}";

                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: partitionKey,
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = apiPermit,
                        Window = apiWindow,
                        SegmentsPerWindow = 4,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
        });

        return services;
    }
}
