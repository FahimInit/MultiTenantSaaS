using SaaS.Domain.Common;

namespace SaaS.Domain.Tenants;

public sealed class TenantSettings : TenantEntity
{
    public const int MinSessionTimeoutMinutes = 5;
    public const int MaxSessionTimeoutMinutes = 43200;

    private TenantSettings()
    {
    }

    public TenantSettings(
        Guid tenantId,
        string billingEmail,
        DateTimeOffset updatedAtUtc,
        string? supportEmail = null,
        string timezone = "UTC",
        string locale = "en-US")
        : base(tenantId)
    {
        Update(
            billingEmail,
            supportEmail,
            timezone,
            locale,
            enforceEmailDomain: false,
            allowedEmailDomains: [],
            sessionTimeoutMinutes: 1440,
            updatedAtUtc);
    }

    public string BillingEmail { get; private set; } = string.Empty;
    public string? SupportEmail { get; private set; }
    public string Timezone { get; private set; } = "UTC";
    public string Locale { get; private set; } = "en-US";
    public bool EnforceEmailDomain { get; private set; }
    public List<string> AllowedEmailDomains { get; private set; } = [];
    public int SessionTimeoutMinutes { get; private set; } = 1440;
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void Update(
        string billingEmail,
        string? supportEmail,
        string timezone,
        string locale,
        bool enforceEmailDomain,
        IEnumerable<string> allowedEmailDomains,
        int sessionTimeoutMinutes,
        DateTimeOffset updatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(billingEmail) || !billingEmail.Contains('@'))
        {
            throw new DomainException("A valid billing email address is required.", "invalid_billing_email");
        }

        if (!string.IsNullOrWhiteSpace(supportEmail) && !supportEmail.Contains('@'))
        {
            throw new DomainException("Support email must be a valid email address.", "invalid_support_email");
        }

        if (sessionTimeoutMinutes is < MinSessionTimeoutMinutes or > MaxSessionTimeoutMinutes)
        {
            throw new DomainException(
                $"Session timeout must be between {MinSessionTimeoutMinutes} and {MaxSessionTimeoutMinutes} minutes.",
                "invalid_session_timeout");
        }

        var normalizedDomains = allowedEmailDomains
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d.Trim().ToLowerInvariant().TrimStart('@'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (enforceEmailDomain && normalizedDomains.Count == 0)
        {
            throw new DomainException(
                "At least one allowed email domain is required when domain enforcement is enabled.",
                "allowed_domains_required");
        }

        BillingEmail = billingEmail.Trim();
        SupportEmail = string.IsNullOrWhiteSpace(supportEmail) ? null : supportEmail.Trim();
        Timezone = string.IsNullOrWhiteSpace(timezone) ? "UTC" : timezone.Trim();
        Locale = string.IsNullOrWhiteSpace(locale) ? "en-US" : locale.Trim();
        EnforceEmailDomain = enforceEmailDomain;
        AllowedEmailDomains = normalizedDomains;
        SessionTimeoutMinutes = sessionTimeoutMinutes;
        UpdatedAtUtc = updatedAtUtc;
    }
}
