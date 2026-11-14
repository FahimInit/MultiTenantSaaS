namespace SaaS.Domain.Common;

/// <summary>
/// Marker interface for domain events raised within aggregate state transitions.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAtUtc { get; }
}

public abstract record DomainEventBase : IDomainEvent
{
    public Guid EventId { get; } = Guid.CreateVersion7();
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
