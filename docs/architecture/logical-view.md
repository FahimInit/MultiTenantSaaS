# 4+1 Logical View

The Logical View describes the functional abstractions, domain aggregates, value objects, business modules, relationships, and domain invariants inside `SaaS.Domain`.

---

## 1. Domain Model & Module Organization

```mermaid
classDiagram
    class Tenant {
        +Guid Id
        +string Name
        +TenantSlug Slug
        +TenantStatus Status
        +Guid CreatedByUserId
        +DateTimeOffset? OnboardingCompletedAtUtc
        +DateTimeOffset CreatedAtUtc
        +Activate()
        +Suspend(string reason)
        +CompleteOnboarding()
        +UpdateProfile(string name, TenantSlug slug)
    }

    class TenantSettings {
        +Guid Id
        +Guid TenantId
        +string BillingEmail
        +string? SupportEmail
        +string Timezone
        +string Locale
        +bool EnforceEmailDomain
        +string[] AllowedEmailDomains
        +int SessionTimeoutMinutes
    }

    class TenantRole {
        +Guid Id
        +Guid TenantId
        +string Code
        +string Name
        +string Description
        +bool IsSystemRole
        +SetPermissions(IEnumerable~string~ permissions)
        +Rename(string name, string description)
    }

    class TenantMembership {
        +Guid Id
        +Guid TenantId
        +Guid UserId
        +Guid RoleId
        +MembershipStatus Status
        +DateTimeOffset JoinedAtUtc
        +ChangeRole(Guid newRoleId)
        +Suspend()
        +Activate()
    }

    class TenantInvitation {
        +Guid Id
        +Guid TenantId
        +string Email
        +Guid RoleId
        +string TokenHash
        +InvitationStatus Status
        +DateTimeOffset ExpiresAtUtc
        +Accept(Guid acceptedByUserId, DateTimeOffset now)
        +Revoke()
        +IsExpired(DateTimeOffset now)
    }

    class SubscriptionPlan {
        +Guid Id
        +string Code
        +string Name
        +long MonthlyPriceCents
        +long AnnualPriceCents
        +int TrialDays
        +int MaxMembers
        +int MaxCustomRoles
        +int MaxMonthlyApiCalls
        +int AuditRetentionDays
        +bool AllowCustomRoles
    }

    class TenantSubscription {
        +Guid Id
        +Guid TenantId
        +Guid PlanId
        +SubscriptionStatus Status
        +BillingCycle BillingCycle
        +DateTimeOffset CurrentPeriodStartUtc
        +DateTimeOffset CurrentPeriodEndUtc
        +DateTimeOffset? TrialEndsAtUtc
        +UpgradeOrChangePlan(SubscriptionPlan newPlan, BillingCycle cycle, DateTimeOffset now)
        +Cancel(DateTimeOffset now)
    }

    class SubscriptionHistory {
        +Guid Id
        +Guid TenantId
        +Guid SubscriptionId
        +Guid? FromPlanId
        +Guid ToPlanId
        +SubscriptionEventType EventType
        +long AmountCents
        +DateTimeOffset OccurredAtUtc
    }

    class TenantUsageSnapshot {
        +Guid Id
        +Guid TenantId
        +DateTimeOffset PeriodStartUtc
        +DateTimeOffset PeriodEndUtc
        +int ActiveMembersCount
        +int CustomRolesCount
        +long ApiCallsCount
    }

    class AuditLog {
        +Guid Id
        +Guid? TenantId
        +Guid? ActorUserId
        +string ActorEmail
        +AuditScope Scope
        +AuditCategory Category
        +string Action
        +string TargetType
        +string TargetId
        +string Description
        +string? MetadataJson
        +DateTimeOffset OccurredAtUtc
    }

    class UserSession {
        +Guid Id
        +Guid UserId
        +string IpAddress
        +string UserAgent
        +DateTimeOffset CreatedAtUtc
        +DateTimeOffset LastSeenAtUtc
        +DateTimeOffset? RevokedAtUtc
        +Revoke(DateTimeOffset now)
    }

    class RefreshToken {
        +Guid Id
        +Guid UserId
        +Guid SessionId
        +Guid TokenFamilyId
        +string TokenHash
        +DateTimeOffset ExpiresAtUtc
        +DateTimeOffset? RevokedAtUtc
        +Guid? ReplacedByTokenId
        +IsActive(DateTimeOffset now)
        +Revoke(string reason, DateTimeOffset now)
    }

    Tenant "1" --> "1" TenantSettings : configures
    Tenant "1" --> "*" TenantRole : defines
    Tenant "1" --> "*" TenantMembership : has
    Tenant "1" --> "*" TenantInvitation : issues
    Tenant "1" --> "1" TenantSubscription : holds
    Tenant "1" --> "*" TenantUsageSnapshot : tracks
    TenantRole "1" --> "*" TenantMembership : assigned_to
    TenantRole "1" --> "*" TenantInvitation : grants
    SubscriptionPlan "1" --> "*" TenantSubscription : defines_terms
    TenantSubscription "1" --> "*" SubscriptionHistory : records
    UserSession "1" --> "*" RefreshToken : owns
```

---

## 2. Core Business Modules & Domain Invariants

| Module | Primary Entities / Value Objects | Key Domain Invariants |
|---|---|---|
| **Identity** | `UserSession`, `RefreshToken` (+ Infra `ApplicationUser`) | Refresh tokens are immutable once revoked; presenting a revoked token invalidates its entire `TokenFamilyId` and owning `UserSession`. |
| **Tenants** | `Tenant`, `TenantSettings`, `TenantSlug` | `TenantSlug` is lowercase alphanumeric + hyphens (`^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$`), globally unique. Suspended/Deactivated tenants cannot execute standard workspace operations. |
| **Memberships** | `TenantMembership`, `TenantInvitation`, `EmailAddress` | Every tenant must maintain at least one `Active` `Organization Owner`. Only an existing Owner can promote/demote another Owner. Pending invitations enforce `ExpiresAtUtc` and email matching on acceptance. |
| **AccessControl** | `TenantRole`, `RolePermission`, `PermissionCatalog` | System roles (`ORG_OWNER`, `ORG_ADMIN`, `MANAGER`, `MEMBER`) cannot be deleted or have their code renamed; `ORG_OWNER` retains immutable full tenant capability set. |
| **Subscriptions** | `SubscriptionPlan`, `TenantSubscription`, `SubscriptionHistory`, `TenantUsageSnapshot` | Downgrades are blocked if current active member count or custom role count exceeds target plan's `MaxMembers` / `MaxCustomRoles`. Subscriptions follow valid state transitions (`Trialing -> Active/Expired/Canceled`, `Active -> Canceled/PastDue`). |
| **Audit** | `AuditLog` | Append-only records; no public update or delete operations exist on `AuditLog`. Sensitive fields are stripped before serialization. |

---

## 3. Domain Events

Domain events (`IDomainEvent`) decouple core aggregate mutations from cross-module side effects within the same transaction or post-commit dispatch:
- `TenantCreatedDomainEvent` -> seeds audit log + onboarding notification
- `TenantSuspendedDomainEvent` -> records critical platform audit entry
- `MemberInvitedDomainEvent` -> sends invitation email via `IEmailService`
- `MembershipRoleChangedDomainEvent` -> invalidates cached permissions (Phase 4) + creates audit log
- `SubscriptionChangedDomainEvent` -> writes `SubscriptionHistory` + audit log
- `RefreshTokenReuseDetectedDomainEvent` -> revokes token family + writes high-severity security audit log
