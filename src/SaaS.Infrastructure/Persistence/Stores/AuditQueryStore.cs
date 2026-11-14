using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Models;
using SaaS.Domain.Audit;

namespace SaaS.Infrastructure.Persistence.Stores;

public sealed class AuditQueryStore : IAuditQueryStore
{
    private readonly ApplicationDbContext _dbContext;

    public AuditQueryStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<AuditLogDto>> QueryTenantLogsAsync(
        Guid tenantId,
        TenantAuditFilterQuery query,
        CancellationToken cancellationToken = default)
    {
        // Preserves AuditLog global query filter AND explicitly filters by tenantId and Tenant scope.
        var baseQuery = _dbContext.AuditLogs
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.Scope == AuditScope.Tenant);

        baseQuery = ApplyCommonFilters(
            baseQuery,
            query.Category,
            query.Action,
            query.Search,
            query.FromUtc,
            query.ToUtc);

        if (query.ActorUserId.HasValue)
        {
            baseQuery = baseQuery.Where(a => a.ActorUserId == query.ActorUserId.Value);
        }

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var sortedQuery = query.IsAscending
            ? baseQuery.OrderBy(a => a.OccurredAtUtc)
            : baseQuery.OrderByDescending(a => a.OccurredAtUtc);

        var items = await sortedQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(a => new AuditLogDto(
                a.Id,
                a.TenantId,
                null,
                a.ActorUserId,
                a.ActorEmail,
                a.Scope,
                a.Category,
                a.Action,
                a.TargetType,
                a.TargetId,
                a.Description,
                a.MetadataJson,
                a.IpAddress,
                a.CorrelationId,
                a.OccurredAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogDto>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<PagedResult<AuditLogDto>> QueryPlatformLogsAsync(
        PlatformAuditFilterQuery query,
        CancellationToken cancellationToken = default)
    {
        // Intentionally bypasses tenant global query filter exclusively for PlatformAdmin audit investigation.
        var baseQuery = _dbContext.AuditLogs
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AsQueryable();

        if (query.TenantId.HasValue)
        {
            baseQuery = baseQuery.Where(a => a.TenantId == query.TenantId.Value);
        }

        if (query.Scope.HasValue)
        {
            baseQuery = baseQuery.Where(a => a.Scope == query.Scope.Value);
        }

        baseQuery = ApplyCommonFilters(
            baseQuery,
            query.Category,
            query.Action,
            query.Search,
            query.FromUtc,
            query.ToUtc);

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var sorted = query.IsAscending
            ? baseQuery.OrderBy(a => a.OccurredAtUtc)
            : baseQuery.OrderByDescending(a => a.OccurredAtUtc);

        var items = await (
            from a in sorted.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            join t in _dbContext.Tenants.AsNoTracking() on a.TenantId equals t.Id into tenantGroup
            from tenant in tenantGroup.DefaultIfEmpty()
            select new AuditLogDto(
                a.Id,
                a.TenantId,
                tenant != null ? tenant.Name : null,
                a.ActorUserId,
                a.ActorEmail,
                a.Scope,
                a.Category,
                a.Action,
                a.TargetType,
                a.TargetId,
                a.Description,
                a.MetadataJson,
                a.IpAddress,
                a.CorrelationId,
                a.OccurredAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogDto>(items, query.Page, query.PageSize, totalCount);
    }

    private static IQueryable<AuditLog> ApplyCommonFilters(
        IQueryable<AuditLog> query,
        AuditCategory? category,
        string? action,
        string? search,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc)
    {
        if (category.HasValue)
        {
            query = query.Where(a => a.Category == category.Value);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var normalizedAction = action.Trim().ToUpperInvariant();
            query = query.Where(a => a.Action.ToUpper() == normalizedAction);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(a =>
                a.ActorEmail.ToLower().Contains(term) ||
                a.Action.ToLower().Contains(term) ||
                a.Description.ToLower().Contains(term) ||
                a.TargetType.ToLower().Contains(term));
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(a => a.OccurredAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(a => a.OccurredAtUtc <= toUtc.Value);
        }

        return query;
    }
}
