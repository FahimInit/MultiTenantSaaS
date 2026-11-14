# Requirements Specification

**Project:** Multi-Tenant SaaS Management Platform  
**Status:** Phase 1 — Accepted Baseline  
**Target Stack:** .NET 9 (`net9.0`, `.NET 10`-ready) + ASP.NET Core Web API + EF Core 9 + PostgreSQL + Next.js (TypeScript)

---

## 1. Purpose & Scope

The Multi-Tenant SaaS Management Platform is a commercial-grade B2B SaaS foundation enabling independent organizations (Tenants) to register, onboard, manage team members, enforce fine-grained Role-Based Access Control (RBAC), manage subscription plans and usage quotas, inspect dashboards, and audit security/business events under strict tenant isolation.

### Actors & Personas

| Actor | Scope | Description |
|---|---|---|
| **Unauthenticated Visitor** | Public | Can register a user account, log in, verify email, request password reset, and preview/accept invitations. |
| **Authenticated User** | User-scoped | Holds a global identity; can belong to zero, one, or multiple organizations, manage personal sessions, and create/onboard a new organization. |
| **Organization Owner** | Tenant-scoped | Highest authority within a tenant; holds all tenant permissions including billing/subscription changes and transferring/managing ownership. |
| **Organization Administrator** | Tenant-scoped | Manages members, invitations, custom roles, settings, and views audit logs within a tenant. |
| **Manager** | Tenant-scoped | Manages day-to-day operational membership tasks (e.g., view members, invite standard members) without modifying billing or critical security settings. |
| **Member** | Tenant-scoped | Standard organization participant with read access to dashboard/workspace context and personal profile/membership view. |
| **Platform Administrator** | Platform-scoped | Global operator who manages subscription plan definitions, monitors platform health/metrics, and suspends/reactivates non-compliant tenants with full audit trails. |

---

## 2. Functional Requirements

### Module A — Identity & Authentication
- **FR-A1 (Registration):** Users can register with email, password, and full name. Passwords must satisfy ASP.NET Core Identity complexity rules and use Identity's password hasher (PBKDF2).
- **FR-A2 (Email Verification):** Registration issues a time-limited, single-use verification token sent via `IEmailService`. Unverified accounts cannot create tenants or access protected workspace endpoints (configurable in dev/test via seeders or explicit verification endpoint).
- **FR-A3 (Login & JWT Issuance):** Valid credentials authenticate via ASP.NET Core Identity, enforce account lockout on repeated failures, update `LastLoginAtUtc`, create a `UserSession`, and issue a short-lived JWT Access Token plus a cryptographic Refresh Token.
- **FR-A4 (Refresh Token Rotation & Reuse Detection):** Refreshing issues a new access + refresh token pair within the same `TokenFamilyId` and revokes the previous token. Presenting an already-revoked refresh token triggers reuse detection, immediately revoking the entire token family and session, and recording a security audit event.
- **FR-A5 (Logout & Session Management):** Users can list their active sessions (device/IP/last seen), revoke an individual session, or log out from the current/all sessions.
- **FR-A6 (Password Reset):** Users can request a password reset token and reset their password, which rotates Identity `SecurityStamp` and revokes all active refresh tokens.

### Module B — Tenant Management
- **FR-B1 (Create Organization):** Any verified authenticated user can create a new tenant (name, unique URL slug). Creation atomically provisions default tenant roles, assigns the creator as `Organization Owner`, creates `TenantSettings`, provisions a default Free/Trial `TenantSubscription`, and logs an audit event.
- **FR-B2 (Tenant Discovery & Switching):** Users can list organizations they actively belong to (with their role per organization) and switch active organization context via validated `X-Tenant-Id` selection.
- **FR-B3 (Organization Profile & Settings):** Authorized members can update organization name/slug and `TenantSettings` (billing email, support email, timezone, locale, session timeout, allowed email domains).
- **FR-B4 (Tenant Onboarding):** Track explicit onboarding completion state (`OnboardingCompletedAtUtc`) and checklist steps (organization profile, settings review, team invitation).
- **FR-B5 (Tenant Lifecycle Status):** Support statuses `Trial`, `Active`, `Suspended`, and `Deactivated`. Suspended or deactivated tenants block standard tenant API mutations/queries with explicit `403 Forbidden` ProblemDetails.

### Module C — User & Membership Management
- **FR-C1 (Invite Members):** Authorized users can invite an email address with an assigned tenant role. Rejects invitations if the tenant's subscription member quota is reached or if a pending invitation already exists for that email in the tenant.
- **FR-C2 (Accept / Revoke / Resend Invitations):** Invitees accept via a secure single-use token (authenticated user email must match invitation email). Authorized admins can revoke or resend pending invitations.
- **FR-C3 (Member Directory):** Paginated, filterable, and sortable list of tenant members showing role, membership status, and join timestamp.
- **FR-C4 (Update Role & Remove Member):** Authorized users can change a member's role or remove them. System must prevent removing or demoting the **last active Organization Owner**, prevent privilege escalation (non-owners cannot assign Owner role or permissions they do not possess), and enforce hierarchy protections.

### Module D — Role & Permission Management (RBAC)
- **FR-D1 (System & Custom Roles):** Every tenant receives seeded system roles (`Organization Owner`, `Organization Administrator`, `Manager`, `Member`) marked immutable for deletion. Tenants on eligible plans can create, update, and delete custom roles.
- **FR-D2 (Capability-Based Permissions):** Roles map to explicit capability strings (e.g., `members.invite`, `billing.manage`, `audit.read`). Every backend endpoint enforces permissions via authorization policies rather than hardcoded frontend checks.
- **FR-D3 (Platform Roles):** `PlatformAdministrator` operates strictly at platform scope via dedicated `/api/v1/platform/...` endpoints and cannot implicitly bypass tenant membership on tenant endpoints.

### Module E — Subscription Management
- **FR-E1 (Subscription Plans):** Platform admins define plans (Free, Pro, Enterprise) with pricing, trial duration, quotas (`MaxMembers`, `MaxCustomRoles`, `MaxMonthlyApiCalls`, `AuditRetentionDays`), and feature flags (`AllowCustomRoles`).
- **FR-E2 (Tenant Subscription Lifecycle):** Support statuses `Trialing`, `Active`, `PastDue`, `Canceled`, `Expired`. Track billing cycle (`Monthly` / `Annual`), period start/end dates, and trial expiry.
- **FR-E3 (Plan Changes & Payment Abstraction):** Tenants can upgrade, downgrade (validating current usage fits target plan quotas), or cancel subscriptions through `IPaymentProvider` (initially backed by `MockPaymentProvider`).
- **FR-E4 (Subscription History & Usage Enforcement):** Record every plan transition in `SubscriptionHistory`. Enforce quota and feature gates at the application boundary before allowing restricted operations.

### Module F — Audit & Activity Logs
- **FR-F1 (Comprehensive Event Capture):** Persist immutable audit records for auth events, tenant lifecycle changes, settings updates, invitations, membership/role mutations, subscription changes, platform admin interventions, and cross-tenant security denials.
- **FR-F2 (Secret Scrubbing):** Audit metadata and logs must never store passwords, raw refresh tokens, JWTs, or verification tokens.
- **FR-F3 (Queryable Audit Trail):** Authorized tenant members can filter/paginate their tenant's audit logs by date range, category, action, and actor; platform admins can query platform-scope and cross-tenant security logs.

### Module G — Dashboard & Reporting
- **FR-G1 (Tenant Dashboard):** Aggregate real backend PostgreSQL data for organization overview, member counts by role, pending invitations, subscription status/renewal countdown, plan quota utilization percentages, and recent audit events.
- **FR-G2 (Platform Admin Dashboard):** Aggregate total tenants by status, total users, subscriptions grouped by plan, estimated MRR from active paid plans, and recent platform/security events.

---

## 3. Non-Functional Requirements

| Category | Requirement | Verification Target |
|---|---|---|
| **Security & Isolation** | Zero cross-tenant data reads or writes across all endpoints, queries, and background tasks. | Automated cross-tenant integration tests with 2+ seeded tenants. |
| **Authentication Integrity** | Short-lived JWTs (15 min), SHA-256 hashed refresh tokens with rotation & reuse revocation, ASP.NET Identity password hashing. | Unit + integration tests verifying token expiry, rotation, and reuse family revocation. |
| **Data Integrity** | ACID transactions for composite workflows; optimistic concurrency on `Tenant`, `TenantMembership`, `TenantRole`, `TenantSubscription`; UTC timestamps exclusively. | PostgreSQL integration tests and schema constraint validation. |
| **Performance** | Prevent N+1 queries; projection queries with `AsNoTracking()`; composite `(tenant_id, ...)` indexes; capped pagination (max 100 items/page). | Query plan inspection & pagination tests. |
| **API Consistency** | RESTful resource design, URL versioning (`/api/v1`), FluentValidation, RFC 9457 `ProblemDetails`, OpenAPI documentation. | Integration tests checking status codes and `ProblemDetails` contracts. |
| **Observability** | Structured Serilog logs with `CorrelationId`, `TenantId`, and `UserId` enrichment; health checks (`/health/live`, `/health/ready`). | Log output verification & health endpoint checks. |
| **Maintainability** | Clean Architecture dependency rules enforced across layers; SOLID compliance; zero EF Core entities exposed in API contracts. | Architecture boundary tests (NetArchTest) & code review. |

---

## 4. Resolved Architectural Decisions (Phase 0 -> Phase 1)

1. **SDK Target (`net9.0` aligned to host SDK `9.0.300`):** Accepted in [ADR-008](../decisions/ADRs/ADR-008-dotnet-sdk-and-openapi-tooling.md). All projects target `net9.0` with EF Core 9 (`Npgsql` 9.x) for immediate local/Docker/CI parity while staying `.NET 10` forward-compatible.
2. **Email Verification Policy:** Public user registration issues a cryptographic verification token routed via `IEmailService` (`DevelopmentEmailService` in dev, SMTP in Phase 4) and confirmed via `POST /api/v1/auth/verify-email`; seeded demo users start with `EmailConfirmed = true`.
3. **Trial Expiry Enforcement Policy:** Expired trials transition to `Expired` subscription status, blocking workspace mutations while keeping billing/plan change and read-only inspection accessible.
