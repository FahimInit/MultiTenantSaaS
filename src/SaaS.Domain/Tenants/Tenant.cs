using SaaS.Domain.Common;

namespace SaaS.Domain.Tenants;

public sealed class Tenant : AggregateRoot
{
    private Tenant()
    {
    }

    private Tenant(string name, TenantSlug slug, Guid createdByUserId, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Organization name is required.", "invalid_tenant_name");
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new DomainException("CreatedByUserId cannot be empty.");
        }

        var trimmedName = name.Trim();
        if (trimmedName.Length is < 2 or > 120)
        {
            throw new DomainException("Organization name must be between 2 and 120 characters.", "invalid_tenant_name");
        }

        Name = trimmedName;
        Slug = slug;
        Status = TenantStatus.Trial;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;

        RaiseDomainEvent(new TenantCreatedDomainEvent(Id, createdByUserId, slug.Value));
    }

    public string Name { get; private set; } = string.Empty;
    public TenantSlug Slug { get; private set; } = null!;
    public TenantStatus Status { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset? OnboardingCompletedAtUtc { get; private set; }
    public string? SuspensionReason { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public bool IsOperational => Status is TenantStatus.Trial or TenantStatus.Active;
    public bool IsOnboardingCompleted => OnboardingCompletedAtUtc.HasValue;

    public static Tenant Create(string name, string rawSlug, Guid createdByUserId, DateTimeOffset utcNow)
    {
        var slug = TenantSlug.Create(rawSlug);
        return new Tenant(name, slug, createdByUserId, utcNow);
    }

    public void UpdateProfile(string name, string rawSlug, DateTimeOffset utcNow)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Organization name is required.", "invalid_tenant_name");
        }

        var trimmed = name.Trim();
        if (trimmed.Length is < 2 or > 120)
        {
            throw new DomainException("Organization name must be between 2 and 120 characters.", "invalid_tenant_name");
        }

        Name = trimmed;
        Slug = TenantSlug.Create(rawSlug);
        UpdatedAtUtc = utcNow;
    }

    public void CompleteOnboarding(DateTimeOffset utcNow)
    {
        if (OnboardingCompletedAtUtc is null)
        {
            OnboardingCompletedAtUtc = utcNow;
            UpdatedAtUtc = utcNow;
        }
    }

    public void Activate(DateTimeOffset utcNow)
    {
        if (Status == TenantStatus.Deactivated)
        {
            throw new DomainException("A deactivated organization cannot be reactivated directly.", "invalid_tenant_status_transition");
        }

        Status = TenantStatus.Active;
        SuspensionReason = null;
        UpdatedAtUtc = utcNow;
    }

    public void Suspend(string reason, DateTimeOffset utcNow)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("A reason is required to suspend an organization.", "suspension_reason_required");
        }

        if (Status == TenantStatus.Deactivated)
        {
            throw new DomainException("Cannot suspend a deactivated organization.", "invalid_tenant_status_transition");
        }

        Status = TenantStatus.Suspended;
        SuspensionReason = reason.Trim();
        UpdatedAtUtc = utcNow;
        RaiseDomainEvent(new TenantSuspendedDomainEvent(Id, SuspensionReason));
    }

    public void Deactivate(DateTimeOffset utcNow)
    {
        Status = TenantStatus.Deactivated;
        UpdatedAtUtc = utcNow;
    }
}
