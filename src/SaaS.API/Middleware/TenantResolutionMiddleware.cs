using Microsoft.AspNetCore.Authorization;
using SaaS.API.Authorization;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Exceptions;
using SaaS.Domain.Audit;
using SaaS.Domain.Tenants;
using Serilog.Context;

namespace SaaS.API.Middleware;

/// <summary>
/// Resolves and cryptographically validates tenant membership from X-Tenant-Id for tenant-scoped endpoints.
/// Fails closed by requiring X-Tenant-Id on any authenticated endpoint not explicitly marked [UserScoped] or [PlatformAdminOnly].
/// </summary>
public sealed class TenantResolutionMiddleware
{
    public const string TenantHeaderName = "X-Tenant-Id";

    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentUser currentUser,
        ITenantContextSetter tenantContextSetter,
        ITenantMembershipResolver membershipResolver,
        IAuditLogger auditLogger)
    {
        tenantContextSetter.Clear();

        var endpoint = context.GetEndpoint();
        if (endpoint is null ||
            endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null ||
            !currentUser.IsAuthenticated)
        {
            await _next(context);
            return;
        }

        if (endpoint.Metadata.GetMetadata<UserScopedAttribute>() is not null ||
            endpoint.Metadata.GetMetadata<PlatformAdminOnlyAttribute>() is not null)
        {
            using (LogContext.PushProperty("UserId", currentUser.UserId))
            {
                await _next(context);
            }

            return;
        }

        var rawTenantId = context.Request.Headers[TenantHeaderName].FirstOrDefault();
        if (!Guid.TryParse(rawTenantId, out var requestedTenantId) || requestedTenantId == Guid.Empty)
        {
            throw new ValidationAppException(
                new Dictionary<string, string[]>
                {
                    [TenantHeaderName] = ["A valid X-Tenant-Id UUID header is required for organization-scoped requests."]
                },
                "Missing or invalid tenant context header.");
        }

        var userId = currentUser.UserId ?? throw new UnauthorizedAppException();

        var resolved = await membershipResolver.ResolveAsync(userId, requestedTenantId, context.RequestAborted);
        if (resolved is null)
        {
            await auditLogger.LogPlatformAsync(
                userId,
                currentUser.Email ?? "unknown",
                AuditCategory.Security,
                "CROSS_TENANT_ACCESS_DENIED",
                "Tenant",
                requestedTenantId.ToString(),
                "Authenticated user attempted to access a tenant without active membership.",
                relatedTenantId: null,
                metadata: new Dictionary<string, object?>
                {
                    ["requestedTenantId"] = requestedTenantId,
                    ["requestPath"] = context.Request.Path.Value
                },
                cancellationToken: context.RequestAborted);

            throw new ForbiddenAppException(
                "You do not have active membership in the requested organization.",
                "tenant_access_denied");
        }

        if (resolved.TenantStatus is TenantStatus.Suspended or TenantStatus.Deactivated)
        {
            throw new ForbiddenAppException(
                $"Organization workspace is currently {resolved.TenantStatus.ToString().ToLowerInvariant()}.",
                "tenant_suspended");
        }

        tenantContextSetter.SetTenant(
            resolved.TenantId,
            resolved.MembershipId,
            resolved.RoleCode,
            resolved.Permissions);

        using (LogContext.PushProperty("TenantId", resolved.TenantId))
        using (LogContext.PushProperty("UserId", userId))
        {
            await _next(context);
        }
    }
}
