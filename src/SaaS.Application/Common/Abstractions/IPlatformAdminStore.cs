using SaaS.Application.Common.Models;
using SaaS.Domain.Subscriptions;
using SaaS.Domain.Tenants;

namespace SaaS.Application.Common.Abstractions;

public sealed record PlatformPlanAdoptionDto(
    string PlanCode,
    string PlanName,
    int TenantCount);

public sealed record PlatformDashboardDto(
    int TotalTenants,
    int ActiveTenants,
    int TrialTenants,
    int SuspendedTenants,
    int TotalUsers,
    int VerifiedUsers,
    long EstimatedMonthlyRecurringRevenueCents,
    IReadOnlyList<PlatformPlanAdoptionDto> PlanAdoption,
    IReadOnlyList<AuditLogDto> RecentSecurityAndAdminEvents);

public sealed record PlatformTenantListItemDto(
    Guid Id,
    string Name,
    string Slug,
    TenantStatus Status,
    bool OnboardingCompleted,
    int ActiveMembersCount,
    string? PlanCode,
    string? PlanName,
    SubscriptionStatus? SubscriptionStatus,
    DateTimeOffset CreatedAtUtc);

public sealed record PlatformTenantDetailDto(
    Guid Id,
    string Name,
    string Slug,
    TenantStatus Status,
    string? SuspensionReason,
    bool OnboardingCompleted,
    DateTimeOffset? OnboardingCompletedAtUtc,
    DateTimeOffset CreatedAtUtc,
    int ActiveMembersCount,
    int SuspendedMembersCount,
    int PendingInvitationsCount,
    int CustomRolesCount,
    string? PlanCode,
    string? PlanName,
    SubscriptionStatus? SubscriptionStatus,
    BillingCycle? BillingCycle,
    DateTimeOffset? CurrentPeriodEndUtc,
    DateTimeOffset? TrialEndsAtUtc,
    long CurrentMonthApiCallsCount);

public sealed record PlatformTenantFilterQuery(
    int Page,
    int PageSize,
    string? Search,
    TenantStatus? Status,
    bool IsAscending);

public interface IPlatformAdminStore
{
    Task<PlatformDashboardDto> GetPlatformDashboardAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<PlatformTenantListItemDto>> ListTenantsAsync(
        PlatformTenantFilterQuery query,
        CancellationToken cancellationToken = default);

    Task<PlatformTenantDetailDto?> GetTenantDetailAsync(
        Guid tenantId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);

    Task<Tenant?> FindTenantForUpdateAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
