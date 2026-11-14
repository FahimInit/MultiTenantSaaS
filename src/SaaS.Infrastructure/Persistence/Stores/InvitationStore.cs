using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Models;
using SaaS.Domain.Invitations;

namespace SaaS.Infrastructure.Persistence.Stores;

public sealed class InvitationStore : IInvitationStore
{
    private readonly ApplicationDbContext _dbContext;

    public InvitationStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<InvitationListItemDto>> ListInvitationsAsync(
        Guid tenantId,
        InvitationFilterQuery query,
        CancellationToken cancellationToken = default)
    {
        var baseQuery =
            from i in _dbContext.TenantInvitations.AsNoTracking()
            join r in _dbContext.TenantRoles.AsNoTracking()
                on new { i.TenantId, i.RoleId } equals new { r.TenantId, RoleId = r.Id }
            join u in _dbContext.Users.AsNoTracking()
                on i.InvitedByUserId equals u.Id
            where i.TenantId == tenantId
            select new
            {
                i.Id,
                i.Email,
                i.RoleId,
                RoleCode = r.Code,
                RoleName = r.Name,
                i.Status,
                InvitedByEmail = u.Email ?? "system",
                i.ExpiresAtUtc,
                i.CreatedAtUtc,
                i.AcceptedAtUtc
            };

        if (query.Status.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.Status == query.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            baseQuery = baseQuery.Where(x =>
                x.Email.ToLower().Contains(term) ||
                x.RoleName.ToLower().Contains(term));
        }

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var sortedQuery = query.IsAscending
            ? baseQuery.OrderBy(x => x.CreatedAtUtc)
            : baseQuery.OrderByDescending(x => x.CreatedAtUtc);

        var items = await sortedQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new InvitationListItemDto(
                x.Id,
                x.Email,
                x.RoleId,
                x.RoleCode,
                x.RoleName,
                x.Status,
                x.InvitedByEmail,
                x.ExpiresAtUtc,
                x.CreatedAtUtc,
                x.AcceptedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<InvitationListItemDto>(items, query.Page, query.PageSize, totalCount);
    }

    public Task<TenantInvitation?> FindByIdAsync(
        Guid tenantId,
        Guid invitationId,
        CancellationToken cancellationToken = default) =>
        _dbContext.TenantInvitations
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Id == invitationId, cancellationToken);

    public Task<TenantInvitation?> FindPendingByEmailAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        var lower = normalizedEmail.Trim().ToLowerInvariant();
        return _dbContext.TenantInvitations
            .FirstOrDefaultAsync(
                i => i.TenantId == tenantId && i.NormalizedEmail == lower && i.Status == InvitationStatus.Pending,
                cancellationToken);
    }

    public async Task<InvitationAcceptanceLookup?> FindForAcceptanceByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        // Intentionally bypasses ITenantContext query filter for user-scoped token acceptance before X-Tenant-Id exists.
        var row = await (
            from i in _dbContext.TenantInvitations.IgnoreQueryFilters()
            join t in _dbContext.Tenants.AsNoTracking() on i.TenantId equals t.Id
            join r in _dbContext.TenantRoles.IgnoreQueryFilters().AsNoTracking()
                on new { i.TenantId, i.RoleId } equals new { r.TenantId, RoleId = r.Id }
            where i.TokenHash == tokenHash
            select new
            {
                Invitation = i,
                TenantName = t.Name,
                TenantSlug = t.Slug.Value,
                RoleCode = r.Code,
                RoleName = r.Name,
                TenantStatus = t.Status
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        return new InvitationAcceptanceLookup(
            row.Invitation,
            row.TenantName,
            row.TenantSlug,
            row.RoleCode,
            row.RoleName,
            row.TenantStatus is Domain.Tenants.TenantStatus.Trial or Domain.Tenants.TenantStatus.Active);
    }

    public Task<int> CountPendingInvitationsAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        _dbContext.TenantInvitations
            .AsNoTracking()
            .CountAsync(i => i.TenantId == tenantId && i.Status == InvitationStatus.Pending, cancellationToken);

    public async Task AddInvitationAsync(TenantInvitation invitation, CancellationToken cancellationToken = default)
    {
        await _dbContext.TenantInvitations.AddAsync(invitation, cancellationToken);
    }
}
