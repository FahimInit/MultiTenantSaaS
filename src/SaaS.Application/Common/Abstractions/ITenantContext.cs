namespace SaaS.Application.Common.Abstractions;

/// <summary>
/// Read-only scoped context describing the verified active tenant and caller permissions for the current request.
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
    Guid? MembershipId { get; }
    string? RoleCode { get; }
    IReadOnlySet<string> Permissions { get; }
    bool IsResolved { get; }
    bool HasPermission(string permission);
}

/// <summary>
/// Restricted setter used exclusively by tenant resolution middleware and isolated background job scopes.
/// Segregated from ITenantContext (ISP) so business handlers cannot spoof tenant context.
/// </summary>
public interface ITenantContextSetter
{
    void SetTenant(Guid tenantId, Guid membershipId, string roleCode, IEnumerable<string> permissions);
    void SetSystemJobTenant(Guid tenantId);
    void Clear();
}
