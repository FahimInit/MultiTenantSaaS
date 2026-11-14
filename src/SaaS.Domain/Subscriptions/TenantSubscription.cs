using SaaS.Domain.Common;

namespace SaaS.Domain.Subscriptions;

public sealed class TenantSubscription : TenantEntity
{
    private TenantSubscription()
    {
    }

    private TenantSubscription(
        Guid tenantId,
        Guid planId,
        SubscriptionStatus status,
        BillingCycle billingCycle,
        DateTimeOffset currentPeriodStartUtc,
        DateTimeOffset currentPeriodEndUtc,
        DateTimeOffset? trialEndsAtUtc,
        bool autoRenew,
        string? externalProviderSubscriptionId,
        DateTimeOffset updatedAtUtc)
        : base(tenantId)
    {
        if (planId == Guid.Empty)
        {
            throw new DomainException("PlanId is required.");
        }

        PlanId = planId;
        Status = status;
        BillingCycle = billingCycle;
        CurrentPeriodStartUtc = currentPeriodStartUtc;
        CurrentPeriodEndUtc = currentPeriodEndUtc;
        TrialEndsAtUtc = trialEndsAtUtc;
        AutoRenew = autoRenew;
        ExternalProviderSubscriptionId = externalProviderSubscriptionId;
        UpdatedAtUtc = updatedAtUtc;
    }

    public Guid PlanId { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public BillingCycle BillingCycle { get; private set; }
    public DateTimeOffset CurrentPeriodStartUtc { get; private set; }
    public DateTimeOffset CurrentPeriodEndUtc { get; private set; }
    public DateTimeOffset? TrialEndsAtUtc { get; private set; }
    public DateTimeOffset? CanceledAtUtc { get; private set; }
    public bool AutoRenew { get; private set; }
    public string? ExternalProviderSubscriptionId { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static TenantSubscription StartTrial(
        Guid tenantId,
        Guid planId,
        int trialDays,
        DateTimeOffset utcNow)
    {
        var days = Math.Max(1, trialDays);
        var trialEnd = utcNow.AddDays(days);

        return new TenantSubscription(
            tenantId,
            planId,
            SubscriptionStatus.Trialing,
            BillingCycle.Monthly,
            utcNow,
            trialEnd,
            trialEnd,
            autoRenew: false,
            externalProviderSubscriptionId: null,
            utcNow);
    }

    public static TenantSubscription CreateActive(
        Guid tenantId,
        Guid planId,
        BillingCycle cycle,
        string? externalProviderSubscriptionId,
        DateTimeOffset utcNow,
        bool autoRenew = true)
    {
        var periodEnd = cycle == BillingCycle.Annual ? utcNow.AddYears(1) : utcNow.AddMonths(1);

        return new TenantSubscription(
            tenantId,
            planId,
            SubscriptionStatus.Active,
            cycle,
            utcNow,
            periodEnd,
            trialEndsAtUtc: null,
            autoRenew: autoRenew,
            externalProviderSubscriptionId: externalProviderSubscriptionId,
            utcNow);
    }

    public bool IsTrialExpiredAt(DateTimeOffset utcNow) =>
        Status == SubscriptionStatus.Trialing && TrialEndsAtUtc.HasValue && TrialEndsAtUtc.Value <= utcNow;

    public bool AllowsWorkspaceWritesAt(DateTimeOffset utcNow) =>
        Status switch
        {
            SubscriptionStatus.Active => true,
            SubscriptionStatus.Trialing => !TrialEndsAtUtc.HasValue || TrialEndsAtUtc.Value > utcNow,
            SubscriptionStatus.Canceled => CurrentPeriodEndUtc > utcNow,
            _ => false
        };

    public void ChangePlan(
        Guid newPlanId,
        BillingCycle newCycle,
        bool isPaidPlan,
        string? externalProviderSubscriptionId,
        DateTimeOffset utcNow)
    {
        if (newPlanId == Guid.Empty)
        {
            throw new DomainException("Target PlanId is required.");
        }

        PlanId = newPlanId;
        BillingCycle = newCycle;
        Status = SubscriptionStatus.Active;
        CurrentPeriodStartUtc = utcNow;
        CurrentPeriodEndUtc = newCycle == BillingCycle.Annual ? utcNow.AddYears(1) : utcNow.AddMonths(1);
        TrialEndsAtUtc = null;
        CanceledAtUtc = null;
        AutoRenew = isPaidPlan;
        ExternalProviderSubscriptionId = externalProviderSubscriptionId;
        UpdatedAtUtc = utcNow;
    }

    public void CancelAutoRenew(DateTimeOffset utcNow)
    {
        if (Status is SubscriptionStatus.Expired or SubscriptionStatus.Canceled)
        {
            throw new DomainException("Subscription is already canceled or expired.", "subscription_already_canceled");
        }

        AutoRenew = false;
        CanceledAtUtc = utcNow;
        Status = SubscriptionStatus.Canceled;
        UpdatedAtUtc = utcNow;
    }

    public void MarkExpired(DateTimeOffset utcNow)
    {
        Status = SubscriptionStatus.Expired;
        AutoRenew = false;
        UpdatedAtUtc = utcNow;
    }
}
