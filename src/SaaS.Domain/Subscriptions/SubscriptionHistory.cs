using SaaS.Domain.Common;

namespace SaaS.Domain.Subscriptions;

public sealed class SubscriptionHistory : TenantEntity
{
    private SubscriptionHistory()
    {
    }

    public SubscriptionHistory(
        Guid tenantId,
        Guid subscriptionId,
        Guid? fromPlanId,
        Guid toPlanId,
        SubscriptionEventType eventType,
        long amountCents,
        string currency,
        string? paymentReference,
        Guid? actorUserId,
        string notes,
        DateTimeOffset occurredAtUtc)
        : base(tenantId)
    {
        if (subscriptionId == Guid.Empty || toPlanId == Guid.Empty)
        {
            throw new DomainException("SubscriptionId and ToPlanId are required.");
        }

        SubscriptionId = subscriptionId;
        FromPlanId = fromPlanId;
        ToPlanId = toPlanId;
        EventType = eventType;
        AmountCents = Math.Max(0, amountCents);
        Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();
        PaymentReference = paymentReference;
        ActorUserId = actorUserId;
        Notes = notes?.Trim() ?? string.Empty;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid SubscriptionId { get; private set; }
    public Guid? FromPlanId { get; private set; }
    public Guid ToPlanId { get; private set; }
    public SubscriptionEventType EventType { get; private set; }
    public long AmountCents { get; private set; }
    public string Currency { get; private set; } = "USD";
    public string? PaymentReference { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string Notes { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
}
