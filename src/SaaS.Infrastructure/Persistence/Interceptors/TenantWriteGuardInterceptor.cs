using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Exceptions;
using SaaS.Domain.Common;

namespace SaaS.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Defense-in-depth interceptor preventing any EF Core write from creating, modifying,
/// or deleting a tenant-scoped entity belonging to a different tenant than the resolved request context.
/// </summary>
public sealed class TenantWriteGuardInterceptor : SaveChangesInterceptor
{
    private readonly ITenantContext _tenantContext;

    public TenantWriteGuardInterceptor(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ValidateTenantBoundaries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ValidateTenantBoundaries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ValidateTenantBoundaries(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<ITenantScoped>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            if (entry.Entity.TenantId == Guid.Empty)
            {
                throw new ForbiddenAppException(
                    $"Cannot persist {entry.Entity.GetType().Name} without a valid TenantId.",
                    "invalid_tenant_scope");
            }

            if (_tenantContext.IsResolved && entry.Entity.TenantId != _tenantContext.TenantId!.Value)
            {
                throw new ForbiddenAppException(
                    $"Cross-tenant write blocked for {entry.Entity.GetType().Name}.",
                    "cross_tenant_write_denied");
            }
        }
    }
}
