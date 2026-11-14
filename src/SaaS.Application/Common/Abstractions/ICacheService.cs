namespace SaaS.Application.Common.Abstractions;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan absoluteExpirationRelativeToNow,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}

public interface ITenantPermissionCacheInvalidator
{
    Task InvalidateUserTenantAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task InvalidateTenantMembersAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
