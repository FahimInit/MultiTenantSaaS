using Microsoft.AspNetCore.Authorization;

namespace SaaS.API.Authorization;

/// <summary>
/// Marks an authenticated endpoint as user-scoped (no X-Tenant-Id header required).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class UserScopedAttribute : Attribute;

/// <summary>
/// Marks an endpoint as strictly platform-administrator scoped (bypasses tenant resolution).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class PlatformAdminOnlyAttribute : AuthorizeAttribute
{
    public const string PolicyName = "PlatformAdminOnly";

    public PlatformAdminOnlyAttribute() : base(PolicyName)
    {
    }
}

/// <summary>
/// Enforces a specific tenant capability permission against the verified ITenantContext.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "Permission:";

    public HasPermissionAttribute(string permission)
        : base($"{PolicyPrefix}{permission}")
    {
        Permission = permission;
    }

    public string Permission { get; }
}
