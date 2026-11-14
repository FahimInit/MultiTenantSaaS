using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Abstractions;
using SaaS.Domain.Memberships;
using SaaS.Infrastructure.Persistence;

namespace SaaS.Infrastructure.MultiTenancy;

public sealed class TenantMembershipResolver : ITenantMembershipResolver
{
    private readonly ApplicationDbContext _dbContext;

    public TenantMembershipResolver(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ResolvedTenantMembership?> ResolveAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var result = await (
            from membership in _dbContext.TenantMemberships.IgnoreQueryFilters().AsNoTracking()
            join tenant in _dbContext.Tenants.AsNoTracking() on membership.TenantId equals tenant.Id
            join role in _dbContext.TenantRoles.IgnoreQueryFilters().AsNoTracking()
                on new { membership.TenantId, RoleId = membership.RoleId }
                equals new { role.TenantId, RoleId = role.Id }
            where membership.UserId == userId
                  && membership.TenantId == tenantId
                  && membership.Status == MembershipStatus.Active
            select new
            {
                MembershipId = membership.Id,
                membership.TenantId,
                TenantStatus = tenant.Status,
                RoleCode = role.Code,
                Permissions = _dbContext.RolePermissions
                    .IgnoreQueryFilters()
                    .Where(p => p.TenantId == tenantId && p.RoleId == role.Id)
                    .Select(p => p.PermissionCode)
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return result is null
            ? null
            : new ResolvedTenantMembership(
                result.TenantId,
                result.MembershipId,
                result.TenantStatus,
                result.RoleCode,
                result.Permissions);
    }
}
