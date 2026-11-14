namespace SaaS.Application.Common.Abstractions;

public interface ITenantUsageTracker
{
    void RecordApiCall(Guid tenantId, DateTimeOffset occurredAtUtc);
    Task FlushPendingUsageAsync(CancellationToken cancellationToken = default);
}
