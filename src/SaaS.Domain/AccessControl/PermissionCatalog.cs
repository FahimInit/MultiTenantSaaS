namespace SaaS.Domain.AccessControl;

public static class SystemRoleCodes
{
    public const string PlatformAdministrator = "PlatformAdministrator";

    public const string OrganizationOwner = "ORG_OWNER";
    public const string OrganizationAdministrator = "ORG_ADMIN";
    public const string Manager = "MANAGER";
    public const string Member = "MEMBER";
}

public static class PermissionCatalog
{
    public const string TenantRead = "tenant.read";
    public const string TenantUpdate = "tenant.update";
    public const string TenantSettingsRead = "tenant.settings.read";
    public const string TenantSettingsUpdate = "tenant.settings.update";

    public const string MembersRead = "members.read";
    public const string MembersInvite = "members.invite";
    public const string MembersUpdate = "members.update";
    public const string MembersRemove = "members.remove";

    public const string RolesRead = "roles.read";
    public const string RolesManage = "roles.manage";

    public const string BillingRead = "billing.read";
    public const string BillingManage = "billing.manage";

    public const string AuditRead = "audit.read";
    public const string DashboardRead = "dashboard.read";

    public static readonly IReadOnlyList<string> AllTenantPermissions =
    [
        TenantRead,
        TenantUpdate,
        TenantSettingsRead,
        TenantSettingsUpdate,
        MembersRead,
        MembersInvite,
        MembersUpdate,
        MembersRemove,
        RolesRead,
        RolesManage,
        BillingRead,
        BillingManage,
        AuditRead,
        DashboardRead
    ];

    private static readonly HashSet<string> ValidTenantPermissionSet =
        new(AllTenantPermissions, StringComparer.OrdinalIgnoreCase);

    public static bool IsValidTenantPermission(string permissionCode) =>
        !string.IsNullOrWhiteSpace(permissionCode) && ValidTenantPermissionSet.Contains(permissionCode);

    public static IReadOnlyList<string> GetDefaultPermissionsForSystemRole(string roleCode) =>
        roleCode switch
        {
            SystemRoleCodes.OrganizationOwner => AllTenantPermissions,
            SystemRoleCodes.OrganizationAdministrator =>
            [
                TenantRead,
                TenantUpdate,
                TenantSettingsRead,
                TenantSettingsUpdate,
                MembersRead,
                MembersInvite,
                MembersUpdate,
                MembersRemove,
                RolesRead,
                RolesManage,
                BillingRead,
                AuditRead,
                DashboardRead
            ],
            SystemRoleCodes.Manager =>
            [
                TenantRead,
                MembersRead,
                MembersInvite,
                RolesRead,
                DashboardRead
            ],
            SystemRoleCodes.Member =>
            [
                TenantRead,
                MembersRead,
                DashboardRead
            ],
            _ => []
        };
}
