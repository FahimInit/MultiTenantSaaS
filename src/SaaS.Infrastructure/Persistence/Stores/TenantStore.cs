using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Abstractions;
using SaaS.Domain.AccessControl;
using SaaS.Domain.Memberships;
using SaaS.Domain.Tenants;

namespace SaaS.Infrastructure.Persistence.Stores;

public sealed class TenantStore : ITenantStore
{
    private readonly ApplicationDbContext _dbContext;

    public TenantStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> SlugExistsAsync(
        string normalizedSlug,
        Guid? excludeTenantId = null,
        CancellationToken cancellationToken = default)
    {
        var targetSlug = TenantSlug.Create(normalizedSlug);

        return _dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(
                t => t.Slug == targetSlug && (!excludeTenantId.HasValue || t.Id != excludeTenantId.Value),
                cancellationToken);
    }

    public async Task AddTenantAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        await _dbContext.Tenants.AddAsync(tenant, cancellationToken);
    }

    public async Task AddTenantSettingsAsync(TenantSettings settings, CancellationToken cancellationToken = default)
    {
        await _dbContext.TenantSettings.AddAsync(settings, cancellationToken);
    }

    public async Task AddTenantRolesAsync(IEnumerable<TenantRole> roles, CancellationToken cancellationToken = default)
    {
        await _dbContext.TenantRoles.AddRangeAsync(roles, cancellationToken);
    }

    public async Task AddMembershipAsync(TenantMembership membership, CancellationToken cancellationToken = default)
    {
        await _dbContext.TenantMemberships.AddAsync(membership, cancellationToken);
    }

    public Task<Tenant?> GetCurrentTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

    public Task<TenantSettings?> GetCurrentTenantSettingsAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        _dbContext.TenantSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);

    public async Task<IReadOnlyList<UserTenantSummaryDto>> ListUserTenantsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // IgnoreQueryFilters is intentional here because the user lists all organizations they belong to across tenants,
        // strictly scoped by m.UserId == userId.
        return await (
            from membership in _dbContext.TenantMemberships.IgnoreQueryFilters().AsNoTracking()
            join tenant in _dbContext.Tenants.AsNoTracking() on membership.TenantId equals tenant.Id
            join role in _dbContext.TenantRoles.IgnoreQueryFilters().AsNoTracking()
                on new { membership.TenantId, RoleId = membership.RoleId }
                equals new { role.TenantId, RoleId = role.Id }
            where membership.UserId == userId
                  && membership.Status == MembershipStatus.Active
                  && tenant.Status != TenantStatus.Deactivated
            orderby tenant.Name
            select new UserTenantSummaryDto(
                tenant.Id,
                tenant.Name,
                tenant.Slug.Value,
                tenant.Status,
                role.Code,
                role.Name,
                tenant.OnboardingCompletedAtUtc.HasValue))
            .ToListAsync(cancellationToken);
    }
}
