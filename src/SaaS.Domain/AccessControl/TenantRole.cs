using SaaS.Domain.Common;

namespace SaaS.Domain.AccessControl;

public sealed class TenantRole : TenantEntity
{
    private readonly List<RolePermission> _permissions = [];

    private TenantRole()
    {
    }

    private TenantRole(
        Guid tenantId,
        string code,
        string name,
        string description,
        bool isSystemRole,
        IEnumerable<string> permissions,
        DateTimeOffset utcNow)
        : base(tenantId)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("Role code is required.", "invalid_role_code");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Role name is required.", "invalid_role_name");
        }

        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        IsSystemRole = isSystemRole;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;

        ReplacePermissionsInternal(permissions);
    }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool IsSystemRole { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public IReadOnlyCollection<RolePermission> Permissions => _permissions.AsReadOnly();

    public static TenantRole CreateCustom(
        Guid tenantId,
        string name,
        string description,
        IEnumerable<string> permissions,
        DateTimeOffset utcNow)
    {
        var generatedCode = $"CUSTOM_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
        return new TenantRole(tenantId, generatedCode, name, description, isSystemRole: false, permissions, utcNow);
    }

    public static IReadOnlyList<TenantRole> CreateSystemRolesForTenant(Guid tenantId, DateTimeOffset utcNow) =>
    [
        new TenantRole(
            tenantId,
            SystemRoleCodes.OrganizationOwner,
            "Organization Owner",
            "Full ownership across organization settings, members, roles, billing, and audit logs.",
            isSystemRole: true,
            PermissionCatalog.GetDefaultPermissionsForSystemRole(SystemRoleCodes.OrganizationOwner),
            utcNow),
        new TenantRole(
            tenantId,
            SystemRoleCodes.OrganizationAdministrator,
            "Organization Administrator",
            "Manages members, roles, organization settings, and audit logs.",
            isSystemRole: true,
            PermissionCatalog.GetDefaultPermissionsForSystemRole(SystemRoleCodes.OrganizationAdministrator),
            utcNow),
        new TenantRole(
            tenantId,
            SystemRoleCodes.Manager,
            "Manager",
            "Manages team invitations and views member directory and dashboards.",
            isSystemRole: true,
            PermissionCatalog.GetDefaultPermissionsForSystemRole(SystemRoleCodes.Manager),
            utcNow),
        new TenantRole(
            tenantId,
            SystemRoleCodes.Member,
            "Member",
            "Standard workspace participant with read access to dashboards and member directory.",
            isSystemRole: true,
            PermissionCatalog.GetDefaultPermissionsForSystemRole(SystemRoleCodes.Member),
            utcNow)
    ];

    public void UpdateDetails(string name, string description, IEnumerable<string> permissions, DateTimeOffset utcNow)
    {
        if (Code == SystemRoleCodes.OrganizationOwner)
        {
            throw new DomainException("Organization Owner role permissions and identity are immutable.", "immutable_system_role");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Role name is required.", "invalid_role_name");
        }

        if (!IsSystemRole)
        {
            Name = name.Trim();
        }

        Description = description?.Trim() ?? string.Empty;
        ReplacePermissionsInternal(permissions);
        UpdatedAtUtc = utcNow;
    }

    private void ReplacePermissionsInternal(IEnumerable<string> permissions)
    {
        var distinct = permissions
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinct.Count == 0)
        {
            throw new DomainException("A role must have at least one permission.", "role_permissions_required");
        }

        _permissions.Clear();
        foreach (var permission in distinct)
        {
            _permissions.Add(new RolePermission(TenantId, Id, permission));
        }
    }
}
