namespace SaaS.Domain.Common;

/// <summary>
/// Marker base class for aggregate roots that own transactional consistency boundaries.
/// </summary>
public abstract class AggregateRoot : Entity
{
    protected AggregateRoot()
    {
    }

    protected AggregateRoot(Guid id) : base(id)
    {
    }
}
