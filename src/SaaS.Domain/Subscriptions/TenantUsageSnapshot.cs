using SaaS.Domain.Common;

namespace SaaS.Domain.Subscriptions;

public sealed class TenantUsageSnapshot : TenantEntity
{
    private TenantUsageSnapshot()
    {
    }

    public TenantUsageSnapshot(
        Guid tenantId,
        DateTimeOffset periodStartUtc,
        DateTimeOffset periodEndUtc,
        int activeMembersCount,
        int customRolesCount,
        long apiCallsCount,
        DateTimeOffset updatedAtUtc)
        : base(tenantId)
    {
        PeriodStartUtc = periodStartUtc;
        PeriodEndUtc = periodEndUtc;
        ActiveMembersCount = Math.Max(0, activeMembersCount);
        CustomRolesCount = Math.Max(0, customRolesCount);
        ApiCallsCount = Math.Max(0, apiCallsCount);
        UpdatedAtUtc = updatedAtUtc;
    }

    public DateTimeOffset PeriodStartUtc { get; private set; }
    public DateTimeOffset PeriodEndUtc { get; private set; }
    public int ActiveMembersCount { get; private set; }
    public int CustomRolesCount { get; private set; }
    public long ApiCallsCount { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static (DateTimeOffset PeriodStartUtc, DateTimeOffset PeriodEndUtc) GetMonthlyPeriod(DateTimeOffset utcNow)
    {
        var utc = utcNow.ToUniversalTime();
        var start = new DateTimeOffset(utc.Year, utc.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddMonths(1);
        return (start, end);
    }

    public void UpdateCounts(int activeMembersCount, int customRolesCount, DateTimeOffset utcNow)
    {
        ActiveMembersCount = Math.Max(0, activeMembersCount);
        CustomRolesCount = Math.Max(0, customRolesCount);
        UpdatedAtUtc = utcNow;
    }

    public void IncrementApiCalls(long delta, DateTimeOffset utcNow)
    {
        if (delta <= 0)
        {
            return;
        }

        ApiCallsCount += delta;
        UpdatedAtUtc = utcNow;
    }
}
