using SaaS.Domain.Common;

namespace SaaS.Domain.Subscriptions;

public sealed class SubscriptionPlan : AggregateRoot
{
    private SubscriptionPlan()
    {
    }

    public SubscriptionPlan(
        string code,
        string name,
        string description,
        long monthlyPriceCents,
        long annualPriceCents,
        int trialDays,
        int? maxMembers,
        int? maxCustomRoles,
        int? maxMonthlyApiCalls,
        int auditRetentionDays,
        bool allowCustomRoles,
        bool isActive,
        bool isPublic,
        int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("Plan code is required.", "invalid_plan_code");
        }

        Code = code.Trim().ToUpperInvariant();
        UpdateDetails(
            name,
            description,
            monthlyPriceCents,
            annualPriceCents,
            trialDays,
            maxMembers,
            maxCustomRoles,
            maxMonthlyApiCalls,
            auditRetentionDays,
            allowCustomRoles,
            isActive,
            isPublic,
            sortOrder);
    }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public long MonthlyPriceCents { get; private set; }
    public long AnnualPriceCents { get; private set; }
    public int TrialDays { get; private set; }
    public int? MaxMembers { get; private set; }
    public int? MaxCustomRoles { get; private set; }
    public int? MaxMonthlyApiCalls { get; private set; }
    public int AuditRetentionDays { get; private set; }
    public bool AllowCustomRoles { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsPublic { get; private set; }
    public int SortOrder { get; private set; }

    public long GetPriceCents(BillingCycle cycle) =>
        cycle == BillingCycle.Annual ? AnnualPriceCents : MonthlyPriceCents;

    public bool IsFreePlan => MonthlyPriceCents == 0 && AnnualPriceCents == 0;

    public void UpdateDetails(
        string name,
        string description,
        long monthlyPriceCents,
        long annualPriceCents,
        int trialDays,
        int? maxMembers,
        int? maxCustomRoles,
        int? maxMonthlyApiCalls,
        int auditRetentionDays,
        bool allowCustomRoles,
        bool isActive,
        bool isPublic,
        int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Plan name is required.", "invalid_plan_name");
        }

        if (monthlyPriceCents < 0 || annualPriceCents < 0)
        {
            throw new DomainException("Plan prices cannot be negative.", "invalid_plan_price");
        }

        if (trialDays < 0)
        {
            throw new DomainException("Trial days cannot be negative.", "invalid_trial_days");
        }

        if (maxMembers is <= 0 || (allowCustomRoles && maxCustomRoles is <= 0) || maxMonthlyApiCalls is <= 0)
        {
            throw new DomainException("Quota limits must be positive integers or null for unlimited.", "invalid_plan_quota");
        }

        if (auditRetentionDays < 1)
        {
            throw new DomainException("Audit retention days must be at least 1.", "invalid_retention_days");
        }

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        MonthlyPriceCents = monthlyPriceCents;
        AnnualPriceCents = annualPriceCents;
        TrialDays = trialDays;
        MaxMembers = maxMembers;
        MaxCustomRoles = allowCustomRoles ? maxCustomRoles : 0;
        MaxMonthlyApiCalls = maxMonthlyApiCalls;
        AuditRetentionDays = auditRetentionDays;
        AllowCustomRoles = allowCustomRoles;
        IsActive = isActive;
        IsPublic = isPublic;
        SortOrder = sortOrder;
    }
}
