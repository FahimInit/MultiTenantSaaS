namespace SaaS.Domain.Common;

/// <summary>
/// Contract implemented by all entities owned by a specific tenant.
/// Used by EF Core global query filters and write-guard interceptors.
/// </summary>
public interface ITenantScoped
{
    Guid TenantId { get; }
}

/// <summary>
/// Base class for tenant-owned entities guaranteeing a non-empty TenantId invariant.
/// </summary>
public abstract class TenantEntity : Entity, ITenantScoped
{
    protected TenantEntity()
    {
    }

    protected TenantEntity(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainException("TenantId cannot be empty for a tenant-scoped entity.");
        }

        TenantId = tenantId;
    }

    protected TenantEntity(Guid id, Guid tenantId) : base(id)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainException("TenantId cannot be empty for a tenant-scoped entity.");
        }

        TenantId = tenantId;
    }

    public Guid TenantId { get; protected set; }
}
