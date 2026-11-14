using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SaaS.Application.Common.Abstractions;
using SaaS.Domain.Subscriptions;
using SaaS.Infrastructure.Persistence;

namespace SaaS.Infrastructure.Usage;

/// <summary>
/// Aggregates per-tenant API call counts in memory with zero lock contention and flushes them
/// atomically to PostgreSQL via ON CONFLICT UPSERT.
/// </summary>
public sealed class BufferedTenantUsageTracker : ITenantUsageTracker
{
    private readonly ConcurrentDictionary<(Guid TenantId, DateTimeOffset PeriodStartUtc), long> _pendingCounts = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<BufferedTenantUsageTracker> _logger;

    public BufferedTenantUsageTracker(
        IServiceScopeFactory scopeFactory,
        IDateTimeProvider dateTimeProvider,
        ILogger<BufferedTenantUsageTracker> logger)
    {
        _scopeFactory = scopeFactory;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public void RecordApiCall(Guid tenantId, DateTimeOffset occurredAtUtc)
    {
        if (tenantId == Guid.Empty)
        {
            return;
        }

        var (periodStart, _) = TenantUsageSnapshot.GetMonthlyPeriod(occurredAtUtc);
        _pendingCounts.AddOrUpdate((tenantId, periodStart), 1, static (_, current) => current + 1);
    }

    public async Task FlushPendingUsageAsync(CancellationToken cancellationToken = default)
    {
        var keys = _pendingCounts.Keys.ToArray();
        if (keys.Length == 0)
        {
            return;
        }

        var batch = new List<(Guid TenantId, DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd, long Delta)>();
        foreach (var key in keys)
        {
            if (_pendingCounts.TryRemove(key, out var delta) && delta > 0)
            {
                batch.Add((key.TenantId, key.PeriodStartUtc, key.PeriodStartUtc.AddMonths(1), delta));
            }
        }

        if (batch.Count == 0)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = _dateTimeProvider.UtcNow;

            foreach (var item in batch)
            {
                var snapshotId = Guid.CreateVersion7();
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO tenant_usage_snapshots
                        (id, tenant_id, period_start_utc, period_end_utc, active_members_count, custom_roles_count, api_calls_count, updated_at_utc)
                    VALUES
                        ({snapshotId}, {item.TenantId}, {item.PeriodStart}, {item.PeriodEnd}, 0, 0, {item.Delta}, {now})
                    ON CONFLICT (tenant_id, period_start_utc)
                    DO UPDATE SET
                        api_calls_count = tenant_usage_snapshots.api_calls_count + EXCLUDED.api_calls_count,
                        updated_at_utc = EXCLUDED.updated_at_utc;
                    """,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush tenant API call usage counts; re-queuing deltas.");
            foreach (var item in batch)
            {
                _pendingCounts.AddOrUpdate(
                    (item.TenantId, item.PeriodStart),
                    item.Delta,
                    (_, current) => current + item.Delta);
            }
        }
    }
}
