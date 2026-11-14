using SaaS.Domain.Common;

namespace SaaS.Domain.AccessControl;

public sealed class RolePermission : ITenantScoped
{
    private RolePermission()
    {
    }

    public RolePermission(Guid tenantId, Guid roleId, string permissionCode)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainException("TenantId cannot be empty.");
        }

        if (roleId == Guid.Empty)
        {
            throw new DomainException("RoleId cannot be empty.");
        }

        if (!PermissionCatalog.IsValidTenantPermission(permissionCode))
        {
            throw new DomainException($"Unknown permission code '{permissionCode}'.", "invalid_permission");
        }

        TenantId = tenantId;
        RoleId = roleId;
        PermissionCode = permissionCode.Trim().ToLowerInvariant();
    }

    public Guid TenantId { get; private set; }
    public Guid RoleId { get; private set; }
    public string PermissionCode { get; private set; } = string.Empty;
}
