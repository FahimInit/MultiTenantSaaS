using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SaaS.Infrastructure.Caching;

public sealed class DistributedCacheHealthCheck : IHealthCheck
{
    private readonly IDistributedCache _distributedCache;

    public DistributedCacheHealthCheck(IDistributedCache distributedCache)
    {
        _distributedCache = distributedCache;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _ = await _distributedCache.GetAsync("saas:health:ping", cancellationToken);
            return HealthCheckResult.Healthy("Distributed cache reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Distributed cache unavailable; falling back to DB.", ex);
        }
    }
}
