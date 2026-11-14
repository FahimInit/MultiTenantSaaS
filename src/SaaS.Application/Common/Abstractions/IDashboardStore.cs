using SaaS.Domain.Subscriptions;
using SaaS.Domain.Tenants;

namespace SaaS.Application.Common.Abstractions;

public sealed record DashboardMemberStatsDto(
    int TotalMembers,
    int ActiveMembers,
    int SuspendedMembers,
    int PendingInvitations,
    int RolesCount,
    int CustomRolesCount);

public sealed record DashboardSubscriptionDto(
    string PlanCode,
    string PlanName,
    SubscriptionStatus Status,
    BillingCycle BillingCycle,
    DateTimeOffset CurrentPeriodEndUtc,
    DateTimeOffset? TrialEndsAtUtc,
    bool AutoRenew,
    bool IsTrialExpired,
    int DaysRemaining);

public sealed record DashboardUsageDto(
    int ActiveMembersUsed,
    int? MaxMembers,
    double? MembersUtilizationPercent,
    int CustomRolesUsed,
    int? MaxCustomRoles,
    double? CustomRolesUtilizationPercent,
    long MonthlyApiCallsUsed,
    int? MaxMonthlyApiCalls,
    double? ApiCallsUtilizationPercent);

public sealed record TenantDashboardDto(
    Guid TenantId,
    string TenantName,
    string TenantSlug,
    TenantStatus TenantStatus,
    bool OnboardingCompleted,
    DashboardMemberStatsDto MemberStats,
    DashboardSubscriptionDto Subscription,
    DashboardUsageDto Usage,
    IReadOnlyList<AuditLogDto> RecentActivities);

public interface IDashboardStore
{
    Task<TenantDashboardDto?> GetTenantDashboardAsync(
        Guid tenantId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);
}
