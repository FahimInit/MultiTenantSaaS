# Milestone Plan & Risk Assessment

## 1. Phased Delivery Roadmap

### Phase 0 — Discovery and Architecture (Current Phase)
- **Deliverables:** Requirements specification, Clean Architecture boundaries, 4+1 View Model + C4 diagrams, Database ER diagram & constraints, Multi-tenancy security model, API contract catalog, Initial ADRs, Milestone & Risk plan.
- **Exit Gate:** User review and approval (including confirmation of `.NET 10` SDK installation vs `net9.0` target).

### Phase 1 — Project Foundation
- **Deliverables:**
  - Initialize Git repository (`main`, `.gitignore`, `.env.example`, `README.md`, `CONTRIBUTING.md`, `CHANGELOG.md`).
  - Create `MultiTenantSaaS.sln` and projects (`src/SaaS.Domain`, `SaaS.Application`, `SaaS.Infrastructure`, `SaaS.API`, and all 3 `tests/` projects).
  - Configure PostgreSQL + Docker Compose (`saas-postgres`, `saas-api`), EF Core `ApplicationDbContext`, Serilog structured logging, RFC 9457 `ProblemDetails`, Swagger/OpenAPI, Health Checks (`/health/live`, `/health/ready`), and NetArchTest dependency guardrails.
  - Add GitHub Actions CI workflow (`.github/workflows/ci.yml`).
- **Exit Gate:** Solution builds with zero warnings, API boots against PostgreSQL in Docker/locally, health checks and Swagger return `200 OK`, architecture boundary tests pass.

### Phase 2 — Identity & Multi-Tenant Foundation (Security Core)
- **Deliverables:**
  - `SaaS.Domain` entities for `Tenant`, `TenantSettings`, `TenantRole`, `RolePermission`, `TenantMembership`, `UserSession`, `RefreshToken`, `AuditLog`.
  - ASP.NET Core Identity integration (`ApplicationUser`), registration, email verification, login, JWT issuance, refresh token rotation + reuse detection, logout, session management, password reset.
  - Tenant creation & onboarding, `X-Tenant-Id` `TenantResolutionMiddleware`, `TenantWriteGuardInterceptor`, Global Query Filters, and composite FKs.
  - Seed two demo tenants (`Acme Corp`, `Globex Labs`) with isolated users and roles.
- **Exit Gate:** Unit tests + PostgreSQL integration tests pass for auth rotation/reuse detection and strict cross-tenant isolation (header spoofing + IDOR).

### Phase 3 — Core Business Modules
- **Deliverables:**
  - Module C: Member invitations (email token hash, expiry, accept/revoke/resend), member directory pagination/sorting/filtering, role updates, removal with last-owner protection.
  - Module D: RBAC permission catalog, system roles, custom role CRUD (plan-gated), `[HasPermission]` backend policy handler, privilege escalation prevention.
  - Module E: `SubscriptionPlan` catalog (`Free`, `Pro`, `Enterprise`), `TenantSubscription`, `SubscriptionHistory`, `IPaymentProvider` + `MockPaymentProvider`, quota enforcement.
  - Module F & G: Immutable `AuditLog` tracking + query endpoint, Tenant Dashboard & Platform Admin endpoints (`/api/v1/platform/*`).
- **Exit Gate:** All domain, application, and PostgreSQL integration tests pass across all 7 modules with verified quota enforcement and audit coverage.

### Phase 4 — Advanced Backend Capabilities
- **Deliverables:**
  - Redis integration (`ICacheService` for `(UserId, TenantId)` membership/permission caching with event-driven invalidation, plus distributed rate limiting).
  - Background workers (`IHostedService`) for invitation expiry sweep, trial expiry transitions, usage snapshot aggregation, and audit retention cleanup with preserved tenant context.
  - Email delivery adapter (`SmtpEmailService` for MailHog/SMTP alongside `DevelopmentEmailService`) and usage tracking middleware.
- **Exit Gate:** Cache invalidation tests, rate limiting tests, and tenant-context-preserving background job tests pass.

### Phase 5 — Frontend Administration Dashboard (`frontend/`)
- **Deliverables:**
  - Next.js + TypeScript + Tailwind + shadcn/ui + React Hook Form + Zod + TanStack Query.
  - Implement all 10 required pages connected directly to live backend APIs:
    1. Login (`/login`)
    2. Registration (`/register`)
    3. Organization Onboarding (`/onboarding`)
    4. Organization Dashboard (`/dashboard`)
    5. Members Management (`/members`)
    6. Roles & Permissions (`/roles`)
    7. Subscription Management (`/subscription`)
    8. Organization Settings (`/settings`)
    9. Activity Logs (`/audit-logs`)
    10. Platform Administration (`/platform`)
  - Clean workspace switcher (`X-Tenant-Id`), automatic single-flight refresh token interceptor, light/dark mode toggle.
- **Exit Gate:** `tsc --noEmit`, lint, `next build`, and end-to-end verification against running backend succeed with zero fabricated dashboard stats.

### Phase 6 — Testing, Hardening & Production Readiness
- **Deliverables:**
  - Complete test matrix review, security audit checklist, PostgreSQL index & `EXPLAIN` query plan review (N+1 audit), full multi-container Docker Compose verification, final CI/CD pipeline verification, and deployment documentation.
- **Exit Gate:** Full test suite green, zero critical security findings, documentation synchronized with code.

---

## 2. Technical Risk Assessment

| Risk | Impact | Likelihood | Mitigation Strategy |
|---|---|---|---|
| **Cross-tenant data leak via missed filter or raw query** | Critical | Medium | 6-layer defense: validated middleware, explicit `(id, tenantId)` queries, EF global query filters, `SaveChangesAsync` write guard, PostgreSQL composite FKs, mandatory cross-tenant integration tests. |
| **Local `.NET 10` SDK absence blocking Phase 1 execution** | High | High (Observed) | Surface mismatch in Phase 0 (`9.0.300` installed locally vs `.NET 10` spec). Confirm install of `.NET 10` SDK or Docker SDK build vs `net9.0` before Phase 1. |
| **Privilege escalation on custom roles or owner demotion** | High | Medium | Domain invariants prevent removing/demoting last active `ORG_OWNER`; handlers prevent assigning permissions caller doesn't hold. |
| **Concurrent refresh token race across frontend tabs** | Medium | Medium | Implement single-flight promise lock in frontend API client so parallel 401s trigger only one `/auth/refresh` request instead of triggering reuse detection falsely. |
| **Stale permission cache after role change (Phase 4)** | High | Medium | Short TTL + deterministic synchronous Redis key eviction on `MembershipRoleChanged`, `MemberRemoved`, and `RolePermissionsUpdated` events. |
| **N+1 query degradation on Members/Audit/Dashboard** | Medium | Medium | Enforce DTO `.Select(...)` projections, `AsNoTracking()`, SQL `COUNT`/`GROUP BY` aggregations, and cap `pageSize <= 100`. |
