using Microsoft.Extensions.Logging;
using SaaS.Application.Common.Abstractions;

namespace SaaS.Infrastructure.Billing;

public sealed class MockPaymentProvider : IPaymentProvider
{
    private readonly ILogger<MockPaymentProvider> _logger;

    public MockPaymentProvider(ILogger<MockPaymentProvider> logger)
    {
        _logger = logger;
    }

    public string ProviderName => "MockPaymentProvider";

    public Task<PaymentChargeResult> ProcessSubscriptionPaymentAsync(
        PaymentChargeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(request.PaymentMethodToken, "tok_declined", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "[MOCK BILLING] Declined payment attempt for tenant {TenantSlug} plan {PlanCode}.",
                request.TenantSlug,
                request.PlanCode);

            return Task.FromResult(new PaymentChargeResult(
                Succeeded: false,
                ExternalSubscriptionId: null,
                PaymentReference: string.Empty,
                FailureReason: "Payment method was declined by the payment processor."));
        }

        var shortId = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var externalSubId = $"mock_sub_{shortId}";
        var paymentRef = $"mock_pay_{shortId}";

        _logger.LogInformation(
            "[MOCK BILLING] Charged {AmountCents} {Currency} for tenant {TenantSlug} ({PlanCode} / {Cycle}) Ref={PaymentReference}",
            request.AmountCents,
            request.Currency,
            request.TenantSlug,
            request.PlanCode,
            request.BillingCycle,
            paymentRef);

        return Task.FromResult(new PaymentChargeResult(
            Succeeded: true,
            ExternalSubscriptionId: externalSubId,
            PaymentReference: paymentRef));
    }

    public Task CancelSubscriptionAsync(string externalSubscriptionId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[MOCK BILLING] Canceled external subscription {ExternalSubscriptionId}.",
            externalSubscriptionId);

        return Task.CompletedTask;
    }
}
