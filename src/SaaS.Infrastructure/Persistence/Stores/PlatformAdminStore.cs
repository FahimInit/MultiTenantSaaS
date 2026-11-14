using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Models;
using SaaS.Domain.Invitations;
using SaaS.Domain.Memberships;
using SaaS.Domain.Subscriptions;
using SaaS.Domain.Tenants;

namespace SaaS.Infrastructure.Persistence.Stores;

public sealed class PlatformAdminStore : IPlatformAdminStore
{
    private readonly ApplicationDbContext _dbContext;

    public PlatformAdminStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PlatformDashboardDto> GetPlatformDashboardAsync(CancellationToken cancellationToken = default)
    {
        var tenantStats = await _dbContext.Tenants
            .AsNoTracking()
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var totalTenants = tenantStats.Sum(x => x.Count);
        var activeTenants = tenantStats.FirstOrDefault(x => x.Status == TenantStatus.Active)?.Count ?? 0;
        var trialTenants = tenantStats.FirstOrDefault(x => x.Status == TenantStatus.Trial)?.Count ?? 0;
        var suspendedTenants = tenantStats.FirstOrDefault(x => x.Status == TenantStatus.Suspended)?.Count ?? 0;

        var totalUsers = await _dbContext.Users.AsNoTracking().CountAsync(cancellationToken);
        var verifiedUsers = await _dbContext.Users.AsNoTracking().CountAsync(u => u.EmailConfirmed, cancellationToken);

        var activeSubscriptions = await (
            from sub in _dbContext.TenantSubscriptions.IgnoreQueryFilters().AsNoTracking()
            join plan in _dbContext.SubscriptionPlans.AsNoTracking() on sub.PlanId equals plan.Id
            select new
            {
                sub.Status,
                sub.BillingCycle,
                PlanCode = plan.Code,
                PlanName = plan.Name,
                plan.MonthlyPriceCents,
                plan.AnnualPriceCents,
                plan.SortOrder
            })
            .ToListAsync(cancellationToken);

        long mrrCents = activeSubscriptions
            .Where(s => s.Status == SubscriptionStatus.Active)
            .Sum(s => s.BillingCycle == BillingCycle.Annual
                ? s.AnnualPriceCents / 12
                : s.MonthlyPriceCents);

        var adoption = activeSubscriptions
            .GroupBy(s => new { s.PlanCode, s.PlanName, s.SortOrder })
            .OrderBy(g => g.Key.SortOrder)
            .Select(g => new PlatformPlanAdoptionDto(g.Key.PlanCode, g.Key.PlanName, g.Count()))
            .ToList();

        var recentEvents = await (
            from a in _dbContext.AuditLogs.IgnoreQueryFilters().AsNoTracking()
            join t in _dbContext.Tenants.AsNoTracking() on a.TenantId equals t.Id into tenantGroup
            from tenant in tenantGroup.DefaultIfEmpty()
            orderby a.OccurredAtUtc descending
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
            .Take(15)
            .ToListAsync(cancellationToken);

        return new PlatformDashboardDto(
            totalTenants,
            activeTenants,
            trialTenants,
            suspendedTenants,
            totalUsers,
            verifiedUsers,
            mrrCents,
            adoption,
            recentEvents);
    }

    public async Task<PagedResult<PlatformTenantListItemDto>> ListTenantsAsync(
        PlatformTenantFilterQuery query,
        CancellationToken cancellationToken = default)
    {
        var baseQuery = _dbContext.Tenants.AsNoTracking().AsQueryable();

        if (query.Status.HasValue)
        {
            baseQuery = baseQuery.Where(t => t.Status == query.Status.Value);
        }

        var tenantRows = await (
            from t in baseQuery
            join sub in _dbContext.TenantSubscriptions.IgnoreQueryFilters().AsNoTracking()
                on t.Id equals sub.TenantId into subGroup
            from s in subGroup.DefaultIfEmpty()
            join plan in _dbContext.SubscriptionPlans.AsNoTracking()
                on s.PlanId equals plan.Id into planGroup
            from p in planGroup.DefaultIfEmpty()
            select new
            {
                t.Id,
                t.Name,
                Slug = t.Slug.Value,
                t.Status,
                OnboardingCompleted = t.OnboardingCompletedAtUtc.HasValue,
                PlanCode = p != null ? p.Code : null,
                PlanName = p != null ? p.Name : null,
                SubscriptionStatus = s != null ? (SubscriptionStatus?)s.Status : null,
                t.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            tenantRows = tenantRows
                .Where(r =>
                    r.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    r.Slug.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var ordered = query.IsAscending
            ? tenantRows.OrderBy(r => r.CreatedAtUtc)
            : tenantRows.OrderByDescending(r => r.CreatedAtUtc);

        var totalCount = tenantRows.Count;
        var pageRows = ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        var pageIds = pageRows.Select(r => r.Id).ToList();
        var memberCounts = await _dbContext.TenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => pageIds.Contains(m.TenantId) && m.Status == MembershipStatus.Active)
            .GroupBy(m => m.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, cancellationToken);

        var items = pageRows
            .Select(r => new PlatformTenantListItemDto(
                r.Id,
                r.Name,
                r.Slug,
                r.Status,
                r.OnboardingCompleted,
                memberCounts.GetValueOrDefault(r.Id, 0),
                r.PlanCode,
                r.PlanName,
                r.SubscriptionStatus,
                r.CreatedAtUtc))
            .ToList();

        return new PagedResult<PlatformTenantListItemDto>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<PlatformTenantDetailDto?> GetTenantDetailAsync(
        Guid tenantId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new
            {
                t.Id,
                t.Name,
                Slug = t.Slug.Value,
                t.Status,
                t.SuspensionReason,
                t.OnboardingCompletedAtUtc,
                t.CreatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (tenant is null)
        {
            return null;
        }

        var activeMembers = await _dbContext.TenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(m => m.TenantId == tenantId && m.Status == MembershipStatus.Active, cancellationToken);

        var suspendedMembers = await _dbContext.TenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(m => m.TenantId == tenantId && m.Status == MembershipStatus.Suspended, cancellationToken);

        var pendingInvites = await _dbContext.TenantInvitations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(i => i.TenantId == tenantId && i.Status == InvitationStatus.Pending, cancellationToken);

        var customRoles = await _dbContext.TenantRoles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(r => r.TenantId == tenantId && !r.IsSystemRole, cancellationToken);

        var subInfo = await (
            from sub in _dbContext.TenantSubscriptions.IgnoreQueryFilters().AsNoTracking()
            join plan in _dbContext.SubscriptionPlans.AsNoTracking() on sub.PlanId equals plan.Id
            where sub.TenantId == tenantId
            select new
            {
                PlanCode = plan.Code,
                PlanName = plan.Name,
                sub.Status,
                sub.BillingCycle,
                sub.CurrentPeriodEndUtc,
                sub.TrialEndsAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        var (periodStart, _) = TenantUsageSnapshot.GetMonthlyPeriod(utcNow);
        var apiCalls = await _dbContext.TenantUsageSnapshots
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.PeriodStartUtc == periodStart)
            .Select(u => u.ApiCallsCount)
            .FirstOrDefaultAsync(cancellationToken);

        return new PlatformTenantDetailDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.Status,
            tenant.SuspensionReason,
            tenant.OnboardingCompletedAtUtc.HasValue,
            tenant.OnboardingCompletedAtUtc,
            tenant.CreatedAtUtc,
            activeMembers,
            suspendedMembers,
            pendingInvites,
            customRoles,
            subInfo?.PlanCode,
            subInfo?.PlanName,
            subInfo?.Status,
            subInfo?.BillingCycle,
            subInfo?.CurrentPeriodEndUtc,
            subInfo?.TrialEndsAtUtc,
            apiCalls);
    }

    public Task<Tenant?> FindTenantForUpdateAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
}
