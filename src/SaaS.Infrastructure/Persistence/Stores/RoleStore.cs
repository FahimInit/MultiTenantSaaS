using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Abstractions;
using SaaS.Domain.AccessControl;
using SaaS.Domain.Invitations;
using SaaS.Domain.Memberships;

namespace SaaS.Infrastructure.Persistence.Stores;

public sealed class RoleStore : IRoleStore
{
    private readonly ApplicationDbContext _dbContext;

    public RoleStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TenantRoleSummaryDto>> ListRolesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var roles = await _dbContext.TenantRoles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.IsSystemRole)
            .ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);

        var memberCounts = await _dbContext.TenantMemberships
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.Status != MembershipStatus.Removed)
            .GroupBy(m => m.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, cancellationToken);

        return roles
            .Select(r => new TenantRoleSummaryDto(
                r.Id,
                r.Code,
                r.Name,
                r.Description,
                r.IsSystemRole,
                memberCounts.GetValueOrDefault(r.Id, 0),
                r.Permissions.Select(p => p.PermissionCode).OrderBy(p => p).ToList(),
                r.CreatedAtUtc,
                r.UpdatedAtUtc))
            .ToList();
    }

    public Task<TenantRole?> FindByIdWithPermissionsAsync(
        Guid tenantId,
        Guid roleId,
        CancellationToken cancellationToken = default) =>
        _dbContext.TenantRoles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == roleId, cancellationToken);

    public Task<bool> RoleNameExistsAsync(
        Guid tenantId,
        string name,
        Guid? excludeRoleId = null,
        CancellationToken cancellationToken = default)
    {
        var lower = name.Trim().ToLowerInvariant();
        return _dbContext.TenantRoles
            .AsNoTracking()
            .AnyAsync(
                r => r.TenantId == tenantId
                     && r.Name.ToLower() == lower
                     && (!excludeRoleId.HasValue || r.Id != excludeRoleId.Value),
                cancellationToken);
    }

    public Task<int> CountCustomRolesAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        _dbContext.TenantRoles
            .AsNoTracking()
            .CountAsync(r => r.TenantId == tenantId && !r.IsSystemRole, cancellationToken);

    public Task<int> CountCustomRolesUnfilteredAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        _dbContext.TenantRoles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(r => r.TenantId == tenantId && !r.IsSystemRole, cancellationToken);

    public async Task<int> CountRoleReferencesAsync(
        Guid tenantId,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var members = await _dbContext.TenantMemberships
            .AsNoTracking()
            .CountAsync(
                m => m.TenantId == tenantId && m.RoleId == roleId && m.Status != MembershipStatus.Removed,
                cancellationToken);

        var pendingInvites = await _dbContext.TenantInvitations
            .AsNoTracking()
            .CountAsync(
                i => i.TenantId == tenantId && i.RoleId == roleId && i.Status == InvitationStatus.Pending,
                cancellationToken);

        return members + pendingInvites;
    }

    public async Task AddRoleAsync(TenantRole role, CancellationToken cancellationToken = default)
    {
        await _dbContext.TenantRoles.AddAsync(role, cancellationToken);
    }

    public void RemoveRole(TenantRole role)
    {
        _dbContext.TenantRoles.Remove(role);
    }
}
