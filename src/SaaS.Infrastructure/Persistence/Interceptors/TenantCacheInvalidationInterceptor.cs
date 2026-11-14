using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SaaS.Application.Common.Abstractions;
using SaaS.Domain.AccessControl;
using SaaS.Domain.Memberships;
using SaaS.Domain.Tenants;

namespace SaaS.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Automatically evicts/rotates distributed authorization caches after any successful EF Core commit
/// touching Tenant status, TenantRole, RolePermission, or TenantMembership, ensuring direct DB writes
/// or background maintenance never leave stale permissions or suspended tenants cached.
/// </summary>
public sealed class TenantCacheInvalidationInterceptor : SaveChangesInterceptor
{
    private readonly ITenantPermissionCacheInvalidator _cacheInvalidator;
    private readonly List<(Guid TenantId, Guid? UserId)> _pendingInvalidations = [];

    public TenantCacheInvalidationInterceptor(ITenantPermissionCacheInvalidator cacheInvalidator)
    {
        _cacheInvalidator = cacheInvalidator;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        CaptureAffectedTenants(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CaptureAffectedTenants(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (result > 0 && _pendingInvalidations.Count > 0)
        {
            FlushAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        else
        {
            _pendingInvalidations.Clear();
        }

        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (result > 0 && _pendingInvalidations.Count > 0)
        {
            await FlushAsync(cancellationToken);
        }
        else
        {
            _pendingInvalidations.Clear();
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _pendingInvalidations.Clear();
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        _pendingInvalidations.Clear();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void CaptureAffectedTenants(DbContext? context)
    {
        _pendingInvalidations.Clear();
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            switch (entry.Entity)
            {
                case Tenant tenant when entry.State is EntityState.Modified or EntityState.Deleted:
                    _pendingInvalidations.Add((tenant.Id, null));
                    break;

                case TenantRole role when entry.State is EntityState.Modified or EntityState.Deleted:
                    _pendingInvalidations.Add((role.TenantId, null));
                    break;

                case RolePermission permission:
                    _pendingInvalidations.Add((permission.TenantId, null));
                    break;

                case TenantMembership membership when entry.State is EntityState.Modified or EntityState.Deleted:
                    _pendingInvalidations.Add((membership.TenantId, membership.UserId));
                    break;
            }
        }
    }

    private async Task FlushAsync(CancellationToken cancellationToken)
    {
        var items = _pendingInvalidations.Distinct().ToArray();
        _pendingInvalidations.Clear();

        var fullTenantIds = items
            .Where(x => x.UserId is null)
            .Select(x => x.TenantId)
            .ToHashSet();

        foreach (var tenantId in fullTenantIds)
        {
            await _cacheInvalidator.InvalidateTenantMembersAsync(tenantId, cancellationToken);
        }

        foreach (var (tenantId, userId) in items.Where(x => x.UserId.HasValue && !fullTenantIds.Contains(x.TenantId)))
        {
            await _cacheInvalidator.InvalidateUserTenantAsync(tenantId, userId!.Value, cancellationToken);
        }
    }
}
