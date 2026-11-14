using SaaS.Application.Common.Abstractions;

namespace SaaS.Infrastructure.MultiTenancy;

/// <summary>
/// Scoped per-request (or per-background-iteration) tenant state holder.
/// Starts unresolved (fail-closed) until verified by TenantResolutionMiddleware.
/// </summary>
public sealed class TenantContext : ITenantContext, ITenantContextSetter
{
    private readonly HashSet<string> _permissions = new(StringComparer.OrdinalIgnoreCase);

    public Guid? TenantId { get; private set; }
    public Guid? MembershipId { get; private set; }
    public string? RoleCode { get; private set; }
    public IReadOnlySet<string> Permissions => _permissions;
    public bool IsResolved => TenantId.HasValue && (MembershipId.HasValue || string.Equals(RoleCode, "SYSTEM_JOB", StringComparison.Ordinal));

    public bool HasPermission(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission) || !IsResolved)
        {
            return false;
        }

        return _permissions.Contains(permission);
    }

    public void SetTenant(Guid tenantId, Guid membershipId, string roleCode, IEnumerable<string> permissions)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        }

        if (membershipId == Guid.Empty)
        {
            throw new ArgumentException("MembershipId cannot be empty.", nameof(membershipId));
        }

        TenantId = tenantId;
        MembershipId = membershipId;
        RoleCode = roleCode;
        _permissions.Clear();

        foreach (var permission in permissions)
        {
            if (!string.IsNullOrWhiteSpace(permission))
            {
                _permissions.Add(permission);
            }
        }
    }

    public void SetSystemJobTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        }

        TenantId = tenantId;
        MembershipId = null;
        RoleCode = "SYSTEM_JOB";
        _permissions.Clear();
    }

    public void Clear()
    {
        TenantId = null;
        MembershipId = null;
        RoleCode = null;
        _permissions.Clear();
    }
}
