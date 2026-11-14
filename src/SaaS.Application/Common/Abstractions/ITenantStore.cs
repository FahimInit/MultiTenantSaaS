using SaaS.Domain.AccessControl;
using SaaS.Domain.Memberships;
using SaaS.Domain.Tenants;

namespace SaaS.Application.Common.Abstractions;

public sealed record UserTenantSummaryDto(
    Guid TenantId,
    string Name,
    string Slug,
    TenantStatus Status,
    string RoleCode,
    string RoleName,
    bool OnboardingCompleted);

public interface ITenantStore
{
    Task<bool> SlugExistsAsync(string normalizedSlug, Guid? excludeTenantId = null, CancellationToken cancellationToken = default);
    Task AddTenantAsync(Tenant tenant, CancellationToken cancellationToken = default);
    Task AddTenantSettingsAsync(TenantSettings settings, CancellationToken cancellationToken = default);
    Task AddTenantRolesAsync(IEnumerable<TenantRole> roles, CancellationToken cancellationToken = default);
    Task AddMembershipAsync(TenantMembership membership, CancellationToken cancellationToken = default);
    Task<Tenant?> GetCurrentTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<TenantSettings?> GetCurrentTenantSettingsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserTenantSummaryDto>> ListUserTenantsAsync(Guid userId, CancellationToken cancellationToken = default);
}
