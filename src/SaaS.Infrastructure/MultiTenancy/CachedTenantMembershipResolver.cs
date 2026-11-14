using SaaS.Application.Common.Abstractions;
using SaaS.Infrastructure.Caching;

namespace SaaS.Infrastructure.MultiTenancy;

/// <summary>
/// Caches resolved tenant memberships & role permissions in Redis/IDistributedCache using per-tenant epochs
/// so permission edits and member removals invalidate instantaneously.
/// </summary>
public sealed class CachedTenantMembershipResolver : ITenantMembershipResolver
{
    private static readonly TimeSpan MembershipCacheTtl = TimeSpan.FromMinutes(2);

    private readonly TenantMembershipResolver _innerResolver;
    private readonly DistributedCacheService _cacheService;

    public CachedTenantMembershipResolver(
        TenantMembershipResolver innerResolver,
        DistributedCacheService cacheService)
    {
        _innerResolver = innerResolver;
        _cacheService = cacheService;
    }

    public async Task<ResolvedTenantMembership?> ResolveAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var epoch = await _cacheService.GetOrCreateTenantEpochAsync(tenantId, cancellationToken);
        var cacheKey = DistributedCacheService.BuildMembershipKey(tenantId, epoch, userId);

        var cached = await _cacheService.GetAsync<ResolvedTenantMembership>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var resolved = await _innerResolver.ResolveAsync(userId, tenantId, cancellationToken);
        if (resolved is not null)
        {
            await _cacheService.SetAsync(cacheKey, resolved, MembershipCacheTtl, cancellationToken);
        }

        return resolved;
    }
}
