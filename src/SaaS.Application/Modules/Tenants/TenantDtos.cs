using SaaS.Domain.Tenants;

namespace SaaS.Application.Modules.Tenants;

public sealed record TenantDetailDto(
    Guid Id,
    string Name,
    string Slug,
    TenantStatus Status,
    bool OnboardingCompleted,
    DateTimeOffset? OnboardingCompletedAtUtc,
    DateTimeOffset CreatedAtUtc,
    string CurrentUserRoleCode,
    IReadOnlyList<string> CurrentUserPermissions);

public sealed record TenantSettingsDto(
    Guid TenantId,
    string BillingEmail,
    string? SupportEmail,
    string Timezone,
    string Locale,
    bool EnforceEmailDomain,
    IReadOnlyList<string> AllowedEmailDomains,
    int SessionTimeoutMinutes,
    DateTimeOffset UpdatedAtUtc);
