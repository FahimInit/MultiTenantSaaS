using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Exceptions;
using SaaS.Domain.AccessControl;

namespace SaaS.Application.Modules.Roles;

public sealed record PermissionCatalogItemDto(
    string Code,
    string Module,
    string Description);

public sealed record ListPermissionsQuery : IQuery<IReadOnlyList<PermissionCatalogItemDto>>;

public sealed record ListRolesQuery : IQuery<IReadOnlyList<TenantRoleSummaryDto>>;

public sealed class ListPermissionsHandler
    : IQueryHandler<ListPermissionsQuery, IReadOnlyList<PermissionCatalogItemDto>>
{
    private static readonly IReadOnlyList<PermissionCatalogItemDto> Catalog =
    [
        new(PermissionCatalog.TenantRead, "Organization", "View organization profile and status."),
        new(PermissionCatalog.TenantUpdate, "Organization", "Update organization name, slug, and onboarding completion."),
        new(PermissionCatalog.TenantSettingsRead, "Settings", "View workspace settings and security policies."),
        new(PermissionCatalog.TenantSettingsUpdate, "Settings", "Modify email domain enforcement, locale, and timeouts."),
        new(PermissionCatalog.MembersRead, "Members", "View member directory and pending invitations."),
        new(PermissionCatalog.MembersInvite, "Members", "Invite new members and resend or revoke invitations."),
        new(PermissionCatalog.MembersUpdate, "Members", "Change member roles and suspend or reactivate members."),
        new(PermissionCatalog.MembersRemove, "Members", "Remove members from the organization."),
        new(PermissionCatalog.RolesRead, "Roles", "View roles and assigned capabilities."),
        new(PermissionCatalog.RolesManage, "Roles", "Create, update, and delete custom roles."),
        new(PermissionCatalog.BillingRead, "Subscription", "View subscription plan, usage quotas, and billing history."),
        new(PermissionCatalog.BillingManage, "Subscription", "Upgrade, downgrade, or cancel the organization subscription."),
        new(PermissionCatalog.AuditRead, "Audit", "Search and view organization activity and security logs."),
        new(PermissionCatalog.DashboardRead, "Dashboard", "View organization KPI metrics and usage utilization.")
    ];

    public Task<IReadOnlyList<PermissionCatalogItemDto>> HandleAsync(
        ListPermissionsQuery query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Catalog);
}

public sealed class ListRolesHandler
    : IQueryHandler<ListRolesQuery, IReadOnlyList<TenantRoleSummaryDto>>
{
    private readonly ITenantContext _tenantContext;
    private readonly IRoleStore _roleStore;

    public ListRolesHandler(ITenantContext tenantContext, IRoleStore roleStore)
    {
        _tenantContext = tenantContext;
        _roleStore = roleStore;
    }

    public Task<IReadOnlyList<TenantRoleSummaryDto>> HandleAsync(
        ListRolesQuery query,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new UnauthorizedAppException();
        return _roleStore.ListRolesAsync(tenantId, cancellationToken);
    }
}
