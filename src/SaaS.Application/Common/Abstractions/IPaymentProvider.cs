using SaaS.Domain.Subscriptions;

namespace SaaS.Application.Common.Abstractions;

public sealed record PaymentChargeRequest(
    Guid TenantId,
    string TenantSlug,
    string PlanCode,
    BillingCycle BillingCycle,
    long AmountCents,
    string Currency,
    string? PaymentMethodToken);

public sealed record PaymentChargeResult(
    bool Succeeded,
    string? ExternalSubscriptionId,
    string PaymentReference,
    string? FailureReason = null);

public interface IPaymentProvider
{
    string ProviderName { get; }

    Task<PaymentChargeResult> ProcessSubscriptionPaymentAsync(
        PaymentChargeRequest request,
        CancellationToken cancellationToken = default);

    Task CancelSubscriptionAsync(
        string externalSubscriptionId,
        CancellationToken cancellationToken = default);
}
