using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Abstractions;
using SaaS.Domain.Audit;
using SaaS.Domain.Invitations;
using SaaS.Domain.Memberships;
using SaaS.Domain.Subscriptions;

namespace SaaS.Infrastructure.Persistence.Stores;

public sealed class DashboardStore : IDashboardStore
{
    private readonly ApplicationDbContext _dbContext;

    public DashboardStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TenantDashboardDto?> GetTenantDashboardAsync(
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
                t.OnboardingCompletedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (tenant is null)
        {
            return null;
        }

        var memberCounts = await _dbContext.TenantMemberships
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.Status != MembershipStatus.Removed)
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var activeMembers = memberCounts.FirstOrDefault(x => x.Status == MembershipStatus.Active)?.Count ?? 0;
        var suspendedMembers = memberCounts.FirstOrDefault(x => x.Status == MembershipStatus.Suspended)?.Count ?? 0;
        var totalMembers = activeMembers + suspendedMembers;

        var pendingInvitations = await _dbContext.TenantInvitations
            .AsNoTracking()
            .CountAsync(i => i.TenantId == tenantId && i.Status == InvitationStatus.Pending, cancellationToken);

        var roleStats = await _dbContext.TenantRoles
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .GroupBy(r => r.IsSystemRole)
            .Select(g => new { IsSystem = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var systemRolesCount = roleStats.FirstOrDefault(x => x.IsSystem)?.Count ?? 0;
        var customRolesCount = roleStats.FirstOrDefault(x => !x.IsSystem)?.Count ?? 0;

        var subData = await (
            from sub in _dbContext.TenantSubscriptions.AsNoTracking()
            join plan in _dbContext.SubscriptionPlans.AsNoTracking() on sub.PlanId equals plan.Id
            where sub.TenantId == tenantId
            select new
            {
                sub.Status,
                sub.BillingCycle,
                sub.CurrentPeriodEndUtc,
                sub.TrialEndsAtUtc,
                sub.AutoRenew,
                PlanCode = plan.Code,
                PlanName = plan.Name,
                plan.MaxMembers,
                plan.MaxCustomRoles,
                plan.MaxMonthlyApiCalls
            })
            .FirstOrDefaultAsync(cancellationToken);

        var (periodStart, _) = TenantUsageSnapshot.GetMonthlyPeriod(utcNow);
        var monthlyApiCalls = await _dbContext.TenantUsageSnapshots
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.PeriodStartUtc == periodStart)
            .Select(u => u.ApiCallsCount)
            .FirstOrDefaultAsync(cancellationToken);

        var recentActivities = await _dbContext.AuditLogs
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.Scope == AuditScope.Tenant)
            .OrderByDescending(a => a.OccurredAtUtc)
            .Take(10)
            .Select(a => new AuditLogDto(
                a.Id,
                a.TenantId,
                tenant.Name,
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

        DashboardSubscriptionDto subscriptionDto;
        DashboardUsageDto usageDto;

        if (subData is not null)
        {
            var isTrialExpired = subData.Status == SubscriptionStatus.Trialing &&
                                 subData.TrialEndsAtUtc.HasValue &&
                                 subData.TrialEndsAtUtc.Value <= utcNow;
            var effectiveEnd = subData.Status == SubscriptionStatus.Trialing && subData.TrialEndsAtUtc.HasValue
                ? subData.TrialEndsAtUtc.Value
                : subData.CurrentPeriodEndUtc;
            var daysRemaining = Math.Max(0, (int)Math.Ceiling((effectiveEnd - utcNow).TotalDays));

            subscriptionDto = new DashboardSubscriptionDto(
                subData.PlanCode,
                subData.PlanName,
                subData.Status,
                subData.BillingCycle,
                subData.CurrentPeriodEndUtc,
                subData.TrialEndsAtUtc,
                subData.AutoRenew,
                isTrialExpired,
                daysRemaining);

            usageDto = new DashboardUsageDto(
                activeMembers,
                subData.MaxMembers,
                CalcPct(activeMembers, subData.MaxMembers),
                customRolesCount,
                subData.MaxCustomRoles,
                CalcPct(customRolesCount, subData.MaxCustomRoles),
                monthlyApiCalls,
                subData.MaxMonthlyApiCalls,
                CalcPct(monthlyApiCalls, subData.MaxMonthlyApiCalls));
        }
        else
        {
            subscriptionDto = new DashboardSubscriptionDto(
                "UNASSIGNED",
                "Unassigned",
                SubscriptionStatus.Expired,
                BillingCycle.Monthly,
                utcNow,
                null,
                false,
                true,
                0);

            usageDto = new DashboardUsageDto(
                activeMembers,
                null,
                null,
                customRolesCount,
                null,
                null,
                monthlyApiCalls,
                null,
                null);
        }

        return new TenantDashboardDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.Status,
            tenant.OnboardingCompletedAtUtc.HasValue,
            new DashboardMemberStatsDto(
                totalMembers,
                activeMembers,
                suspendedMembers,
                pendingInvitations,
                systemRolesCount + customRolesCount,
                customRolesCount),
            subscriptionDto,
            usageDto,
            recentActivities);
    }

    private static double? CalcPct(long used, int? max)
    {
        if (!max.HasValue || max.Value <= 0)
        {
            return null;
        }

        return Math.Round(used * 100.0 / max.Value, 1, MidpointRounding.AwayFromZero);
    }
}
