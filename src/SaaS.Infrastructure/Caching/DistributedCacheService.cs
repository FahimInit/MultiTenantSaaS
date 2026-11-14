using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using SaaS.Application.Common.Abstractions;

namespace SaaS.Infrastructure.Caching;

public sealed class DistributedCacheService : ICacheService, ITenantPermissionCacheInvalidator
{
    private static readonly TimeSpan VersionTtl = TimeSpan.FromHours(12);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<DistributedCacheService> _logger;

    public DistributedCacheService(
        IDistributedCache distributedCache,
        ILogger<DistributedCacheService> logger)
    {
        _distributedCache = distributedCache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var raw = await _distributedCache.GetStringAsync(key, cancellationToken);
            if (string.IsNullOrEmpty(raw))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(raw, SerializerOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache read failed for key {CacheKey}; falling back to database.", key);
            return default;
        }
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan absoluteExpirationRelativeToNow,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(value, SerializerOptions);
            await _distributedCache.SetStringAsync(
                key,
                json,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = absoluteExpirationRelativeToNow },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache write failed for key {CacheKey}; continuing without cache.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _distributedCache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache eviction failed for key {CacheKey}.", key);
        }
    }

    public async Task<string> GetOrCreateTenantEpochAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var epochKey = BuildTenantEpochKey(tenantId);
        var current = await GetAsync<string>(epochKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(current))
        {
            return current;
        }

        var created = Guid.NewGuid().ToString("N");
        await SetAsync(epochKey, created, VersionTtl, cancellationToken);
        return created;
    }

    public async Task InvalidateUserTenantAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var epochKey = BuildTenantEpochKey(tenantId);
        var epoch = await GetAsync<string>(epochKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(epoch))
        {
            await RemoveAsync(BuildMembershipKey(tenantId, epoch, userId), cancellationToken);
        }
    }

    public async Task InvalidateTenantMembersAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        // O(1) epoch rotation atomically invalidates every cached membership key for this tenant across nodes.
        var nextEpoch = Guid.NewGuid().ToString("N");
        await SetAsync(BuildTenantEpochKey(tenantId), nextEpoch, VersionTtl, cancellationToken);
    }

    public static string BuildTenantEpochKey(Guid tenantId) => $"saas:tenant:{tenantId:N}:authz-epoch";
    public static string BuildMembershipKey(Guid tenantId, string epoch, Guid userId) =>
        $"saas:tenant:{tenantId:N}:e:{epoch}:member:{userId:N}";
}
