using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Models;
using SaaS.Domain.AccessControl;
using SaaS.Domain.Memberships;

namespace SaaS.Infrastructure.Persistence.Stores;

public sealed class MembershipStore : IMembershipStore
{
    private readonly ApplicationDbContext _dbContext;

    public MembershipStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<MemberListItemDto>> ListMembersAsync(
        Guid tenantId,
        MemberFilterQuery query,
        CancellationToken cancellationToken = default)
    {
        var baseQuery =
            from m in _dbContext.TenantMemberships.AsNoTracking()
            join u in _dbContext.Users.AsNoTracking() on m.UserId equals u.Id
            join r in _dbContext.TenantRoles.AsNoTracking()
                on new { m.TenantId, m.RoleId } equals new { r.TenantId, RoleId = r.Id }
            where m.TenantId == tenantId && m.Status != MembershipStatus.Removed
            select new
            {
                m.Id,
                m.UserId,
                Email = u.Email ?? string.Empty,
                u.FullName,
                m.RoleId,
                RoleCode = r.Code,
                RoleName = r.Name,
                r.IsSystemRole,
                m.Status,
                m.JoinedAtUtc,
                u.LastLoginAtUtc
            };

        if (query.Status.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.Status == query.Status.Value);
        }

        if (query.RoleId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.RoleId == query.RoleId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            baseQuery = baseQuery.Where(x =>
                x.Email.ToLower().Contains(term) ||
                x.FullName.ToLower().Contains(term) ||
                x.RoleName.ToLower().Contains(term));
        }

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var sortedQuery = (query.SortBy?.ToLowerInvariant(), query.IsAscending) switch
        {
            ("email", true) => baseQuery.OrderBy(x => x.Email),
            ("email", false) => baseQuery.OrderByDescending(x => x.Email),
            ("name" or "fullname", true) => baseQuery.OrderBy(x => x.FullName),
            ("name" or "fullname", false) => baseQuery.OrderByDescending(x => x.FullName),
            ("status", true) => baseQuery.OrderBy(x => x.Status),
            ("status", false) => baseQuery.OrderByDescending(x => x.Status),
            (_, true) => baseQuery.OrderBy(x => x.JoinedAtUtc),
            _ => baseQuery.OrderByDescending(x => x.JoinedAtUtc)
        };

        var items = await sortedQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new MemberListItemDto(
                x.Id,
                x.UserId,
                x.Email,
                x.FullName,
                x.RoleId,
                x.RoleCode,
                x.RoleName,
                x.IsSystemRole,
                x.Status,
                x.JoinedAtUtc,
                x.LastLoginAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<MemberListItemDto>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<MemberDetailWithRole?> FindMemberDetailAsync(
        Guid tenantId,
        Guid membershipId,
        CancellationToken cancellationToken = default)
    {
        var membership = await _dbContext.TenantMemberships
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == membershipId, cancellationToken);

        if (membership is null)
        {
            return null;
        }

        var metadata = await (
            from u in _dbContext.Users.AsNoTracking()
            from r in _dbContext.TenantRoles.IgnoreQueryFilters().AsNoTracking()
            where u.Id == membership.UserId
                  && r.TenantId == tenantId
                  && r.Id == membership.RoleId
            select new
            {
                Email = u.Email ?? string.Empty,
                u.FullName,
                RoleCode = r.Code,
                RoleName = r.Name
            })
            .FirstOrDefaultAsync(cancellationToken);

        return metadata is null
            ? null
            : new MemberDetailWithRole(
                membership,
                metadata.Email,
                metadata.FullName,
                metadata.RoleCode,
                metadata.RoleName);
    }

    public Task<TenantMembership?> FindByTenantAndUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        _dbContext.TenantMemberships
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.UserId == userId, cancellationToken);

    public Task<TenantMembership?> FindByTenantAndUserUnfilteredAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        _dbContext.TenantMemberships
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.UserId == userId, cancellationToken);

    public Task<bool> HasActiveOrSuspendedMemberByEmailAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        var upper = normalizedEmail.Trim().ToUpperInvariant();

        return (
            from m in _dbContext.TenantMemberships.AsNoTracking()
            join u in _dbContext.Users.AsNoTracking() on m.UserId equals u.Id
            where m.TenantId == tenantId
                  && m.Status != MembershipStatus.Removed
                  && u.NormalizedEmail == upper
            select m.Id)
            .AnyAsync(cancellationToken);
    }

    public Task<int> CountActiveMembersAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        _dbContext.TenantMemberships
            .AsNoTracking()
            .CountAsync(m => m.TenantId == tenantId && m.Status == MembershipStatus.Active, cancellationToken);

    public Task<int> CountActiveMembersUnfilteredAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        _dbContext.TenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(m => m.TenantId == tenantId && m.Status == MembershipStatus.Active, cancellationToken);

    public Task<int> CountActiveOwnersAsync(
        Guid tenantId,
        Guid? excludeMembershipId = null,
        CancellationToken cancellationToken = default)
    {
        return (
            from m in _dbContext.TenantMemberships.AsNoTracking()
            join r in _dbContext.TenantRoles.AsNoTracking()
                on new { m.TenantId, m.RoleId } equals new { r.TenantId, RoleId = r.Id }
            where m.TenantId == tenantId
                  && m.Status == MembershipStatus.Active
                  && r.Code == SystemRoleCodes.OrganizationOwner
                  && (!excludeMembershipId.HasValue || m.Id != excludeMembershipId.Value)
            select m.Id)
            .CountAsync(cancellationToken);
    }
}
