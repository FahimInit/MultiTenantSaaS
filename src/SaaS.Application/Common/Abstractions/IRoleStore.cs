using SaaS.Domain.AccessControl;

namespace SaaS.Application.Common.Abstractions;

public sealed record TenantRoleSummaryDto(
    Guid Id,
    string Code,
    string Name,
    string Description,
    bool IsSystemRole,
    int MemberCount,
    IReadOnlyList<string> Permissions,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public interface IRoleStore
{
    Task<IReadOnlyList<TenantRoleSummaryDto>> ListRolesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<TenantRole?> FindByIdWithPermissionsAsync(
        Guid tenantId,
        Guid roleId,
        CancellationToken cancellationToken = default);

    Task<bool> RoleNameExistsAsync(
        Guid tenantId,
        string name,
        Guid? excludeRoleId = null,
        CancellationToken cancellationToken = default);

    Task<int> CountCustomRolesAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<int> CountCustomRolesUnfilteredAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<int> CountRoleReferencesAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken = default);

    Task AddRoleAsync(TenantRole role, CancellationToken cancellationToken = default);

    void RemoveRole(TenantRole role);
}
