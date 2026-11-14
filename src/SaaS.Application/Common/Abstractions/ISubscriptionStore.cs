using SaaS.Application.Common.Models;
using SaaS.Domain.Subscriptions;

namespace SaaS.Application.Common.Abstractions;

public sealed record SubscriptionPlanDto(
    Guid Id,
    string Code,
    string Name,
    string Description,
    long MonthlyPriceCents,
    long AnnualPriceCents,
    int TrialDays,
    int? MaxMembers,
    int? MaxCustomRoles,
    int? MaxMonthlyApiCalls,
    int AuditRetentionDays,
    bool AllowCustomRoles,
    bool IsActive,
    bool IsPublic,
    int SortOrder);

public sealed record SubscriptionHistoryDto(
    Guid Id,
    Guid? FromPlanId,
    string? FromPlanName,
    Guid ToPlanId,
    string ToPlanName,
    SubscriptionEventType EventType,
    long AmountCents,
    string Currency,
    string? PaymentReference,
    Guid? ActorUserId,
    string Notes,
    DateTimeOffset OccurredAtUtc);

public sealed record TenantSubscriptionWithPlan(
    TenantSubscription Subscription,
    SubscriptionPlan Plan);

public interface ISubscriptionStore
{
    Task<IReadOnlyList<SubscriptionPlanDto>> ListPlansAsync(
        bool publicAndActiveOnly,
        CancellationToken cancellationToken = default);

    Task<SubscriptionPlan?> FindPlanByIdAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<SubscriptionPlan?> FindPlanByCodeAsync(string planCode, CancellationToken cancellationToken = default);
    Task<bool> PlanCodeExistsAsync(string normalizedCode, Guid? excludePlanId = null, CancellationToken cancellationToken = default);
    Task AddPlanAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default);

    Task<TenantSubscriptionWithPlan?> GetTenantSubscriptionWithPlanAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Intentionally bypasses ITenantContext query filter for user-scoped invitation acceptance
    /// before X-Tenant-Id context exists.
    /// </summary>
    Task<TenantSubscriptionWithPlan?> GetTenantSubscriptionWithPlanUnfilteredAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task AddTenantSubscriptionAsync(TenantSubscription subscription, CancellationToken cancellationToken = default);
    Task AddSubscriptionHistoryAsync(SubscriptionHistory history, CancellationToken cancellationToken = default);

    Task<PagedResult<SubscriptionHistoryDto>> ListSubscriptionHistoryAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<TenantUsageSnapshot?> GetUsageSnapshotAsync(
        Guid tenantId,
        DateTimeOffset periodStartUtc,
        CancellationToken cancellationToken = default);

    Task<TenantUsageSnapshot?> GetUsageSnapshotUnfilteredAsync(
        Guid tenantId,
        DateTimeOffset periodStartUtc,
        CancellationToken cancellationToken = default);

    Task AddUsageSnapshotAsync(TenantUsageSnapshot snapshot, CancellationToken cancellationToken = default);
}
