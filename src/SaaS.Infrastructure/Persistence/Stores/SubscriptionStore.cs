using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Models;
using SaaS.Domain.Subscriptions;

namespace SaaS.Infrastructure.Persistence.Stores;

public sealed class SubscriptionStore : ISubscriptionStore
{
    private readonly ApplicationDbContext _dbContext;

    public SubscriptionStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<SubscriptionPlanDto>> ListPlansAsync(
        bool publicAndActiveOnly,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.SubscriptionPlans.AsNoTracking();

        if (publicAndActiveOnly)
        {
            query = query.Where(p => p.IsActive && p.IsPublic);
        }

        return await query
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.MonthlyPriceCents)
            .Select(p => new SubscriptionPlanDto(
                p.Id,
                p.Code,
                p.Name,
                p.Description,
                p.MonthlyPriceCents,
                p.AnnualPriceCents,
                p.TrialDays,
                p.MaxMembers,
                p.MaxCustomRoles,
                p.MaxMonthlyApiCalls,
                p.AuditRetentionDays,
                p.AllowCustomRoles,
                p.IsActive,
                p.IsPublic,
                p.SortOrder))
            .ToListAsync(cancellationToken);
    }

    public Task<SubscriptionPlan?> FindPlanByIdAsync(Guid planId, CancellationToken cancellationToken = default) =>
        _dbContext.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == planId, cancellationToken);

    public Task<SubscriptionPlan?> FindPlanByCodeAsync(string planCode, CancellationToken cancellationToken = default)
    {
        var upper = planCode.Trim().ToUpperInvariant();
        return _dbContext.SubscriptionPlans.FirstOrDefaultAsync(p => p.Code == upper, cancellationToken);
    }

    public Task<bool> PlanCodeExistsAsync(
        string normalizedCode,
        Guid? excludePlanId = null,
        CancellationToken cancellationToken = default)
    {
        var upper = normalizedCode.Trim().ToUpperInvariant();
        return _dbContext.SubscriptionPlans
            .AsNoTracking()
            .AnyAsync(
                p => p.Code == upper && (!excludePlanId.HasValue || p.Id != excludePlanId.Value),
                cancellationToken);
    }

    public async Task AddPlanAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default)
    {
        await _dbContext.SubscriptionPlans.AddAsync(plan, cancellationToken);
    }

    public async Task<TenantSubscriptionWithPlan?> GetTenantSubscriptionWithPlanAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await (
            from sub in _dbContext.TenantSubscriptions
            join plan in _dbContext.SubscriptionPlans on sub.PlanId equals plan.Id
            where sub.TenantId == tenantId
            select new TenantSubscriptionWithPlan(sub, plan))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TenantSubscriptionWithPlan?> GetTenantSubscriptionWithPlanUnfilteredAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await (
            from sub in _dbContext.TenantSubscriptions.IgnoreQueryFilters()
            join plan in _dbContext.SubscriptionPlans on sub.PlanId equals plan.Id
            where sub.TenantId == tenantId
            select new TenantSubscriptionWithPlan(sub, plan))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddTenantSubscriptionAsync(
        TenantSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.TenantSubscriptions.AddAsync(subscription, cancellationToken);
    }

    public async Task AddSubscriptionHistoryAsync(
        SubscriptionHistory history,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.SubscriptionHistory.AddAsync(history, cancellationToken);
    }

    public async Task<PagedResult<SubscriptionHistoryDto>> ListSubscriptionHistoryAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var baseQuery =
            from h in _dbContext.SubscriptionHistory.AsNoTracking()
            join toPlan in _dbContext.SubscriptionPlans.AsNoTracking() on h.ToPlanId equals toPlan.Id
            join fromPlan in _dbContext.SubscriptionPlans.AsNoTracking()
                on h.FromPlanId equals fromPlan.Id into fromGroup
            from fp in fromGroup.DefaultIfEmpty()
            where h.TenantId == tenantId
            select new
            {
                h.Id,
                h.FromPlanId,
                FromPlanName = fp != null ? fp.Name : null,
                h.ToPlanId,
                ToPlanName = toPlan.Name,
                h.EventType,
                h.AmountCents,
                h.Currency,
                h.PaymentReference,
                h.ActorUserId,
                h.Notes,
                h.OccurredAtUtc
            };

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var items = await baseQuery
            .OrderByDescending(x => x.OccurredAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SubscriptionHistoryDto(
                x.Id,
                x.FromPlanId,
                x.FromPlanName,
                x.ToPlanId,
                x.ToPlanName,
                x.EventType,
                x.AmountCents,
                x.Currency,
                x.PaymentReference,
                x.ActorUserId,
                x.Notes,
                x.OccurredAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<SubscriptionHistoryDto>(items, page, pageSize, totalCount);
    }

    public Task<TenantUsageSnapshot?> GetUsageSnapshotAsync(
        Guid tenantId,
        DateTimeOffset periodStartUtc,
        CancellationToken cancellationToken = default) =>
        _dbContext.TenantUsageSnapshots
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.PeriodStartUtc == periodStartUtc, cancellationToken);

    public Task<TenantUsageSnapshot?> GetUsageSnapshotUnfilteredAsync(
        Guid tenantId,
        DateTimeOffset periodStartUtc,
        CancellationToken cancellationToken = default) =>
        _dbContext.TenantUsageSnapshots
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.PeriodStartUtc == periodStartUtc, cancellationToken);

    public async Task AddUsageSnapshotAsync(
        TenantUsageSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.TenantUsageSnapshots.AddAsync(snapshot, cancellationToken);
    }
}
