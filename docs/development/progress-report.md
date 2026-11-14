# Project Progress Report

## Phase 2 — Identity and Multi-Tenant Foundation
**Status:** Completed  
**Date:** 2026-11-15

### What Was Implemented
- **Domain Entities & Catalogs (`SaaS.Domain`):**
  - `Identity`: `UserSession`, `RefreshToken` (state transitions, active/expired/revoked tracking).
  - `Tenants`: `Tenant` (`Active`/`Suspended`/`Pending`), `TenantSlug` (regex normalization + reserved slug validation), `TenantSettings` (timezone, locale, allowed email domains with enforcement invariant), domain events.
  - `AccessControl`: `TenantRole` (system role immutability checks), `RolePermission`, `PermissionCatalog` (5 core roles: Platform Administrator, Organization Owner, Organization Administrator, Manager, Member; 14 granular tenant capabilities).
  - `Memberships`: `TenantMembership` (tenant-scoped membership linking users and roles).
  - `Audit`: `AuditLog` (`AuditScope` Platform/Tenant, `AuditCategory`, immutable factories `CreateTenantEvent` / `CreatePlatformEvent`).
- **Application Use Cases & Ports (`SaaS.Application`):**
  - Abstractions: `IIdentityService`, `IJwtTokenGenerator`, `IEmailService`, `IAuditLogger`, `ITenantMembershipResolver`, `ISessionStore`, `ITenantStore`.
  - Identity module: `RegisterUser`, `VerifyEmail`, `Login`, `RefreshToken`, `Logout`, `PasswordReset`, `SessionsAndProfile`.
  - Tenants module: `CreateTenant`, `ManageTenants`, `TenantSettingsUseCases`.
- **Infrastructure & Persistence (`SaaS.Infrastructure`):**
  - EF Core configurations with `AddIdentityAndTenantFoundation` migration: composite alternate key `(tenant_id, id)` on `tenant_roles`, composite FK `(tenant_id, role_id)` on `tenant_memberships`, `xmin` optimistic concurrency tokens, `jsonb` metadata column, fail-closed `AuditLog` global query filter.
  - `JwtTokenGenerator` (HMAC-SHA256 access tokens + `RandomNumberGenerator` opaque refresh tokens), `IdentityService`, `SessionStore`, `TenantStore`, `TenantMembershipResolver`, `AuditLogger` (sensitive key redaction), `DevelopmentEmailService`, and `DatabaseSeeder` (Platform Admin, `Acme Corp`, `Globex Labs`, multi-tenant consultant).
- **API & Multi-Tenant Enforcement (`SaaS.API`):**
  - `AuthExtensions` (`OnTokenValidated` verifies active `UserSession` in DB on every request).
  - `[UserScoped]`, `[PlatformAdminOnly]`, `[HasPermission]` authorization attributes & dynamic permission policy provider/handler.
  - `TenantResolutionMiddleware`: enforces `X-Tenant-Id` on tenant-scoped endpoints, verifies active membership in PostgreSQL, returns `403`/`400` ProblemDetails cleanly, and logs denied cross-tenant access attempts.
  - `AuthController` (`/api/v1/auth/...`) and `TenantsController` (`/api/v1/tenants/...`).

### Tests Executed (Post Final Code Edit)
- `dotnet build MultiTenantSaaS.sln --no-restore -p:UseSharedCompilation=false -m:1`: **0 Warnings, 0 Errors**.
- `dotnet test MultiTenantSaaS.sln --no-build --verbosity normal` (with Testcontainers PostgreSQL 17):
  - `SaaS.Domain.UnitTests`: **19/19 Passed**
  - `SaaS.Application.UnitTests`: **6/6 Passed**
  - `SaaS.API.IntegrationTests`: **21/21 Passed**
  - **Total:** **46/46 Passed**, **0 Failed**, **0 Skipped**.

### Known Limitations (Expected at Phase 2)
- Employee invitations, member role updates/removals, custom role CRUD, subscription plans & `MockPaymentProvider`, audit log search endpoints, and tenant/platform dashboards are scheduled for **Phase 3**.
- Redis caching, background hosted jobs (outbox/usage/token cleanup), and Next.js frontend are scheduled for **Phases 4–5**.

### Next Milestone
- **Phase 3 — Core Business Modules:** Implement Invitations & Membership management, Custom Roles CRUD, Subscriptions & Usage Tracking with `MockPaymentProvider`, Audit Logs querying, and Tenant & Platform Dashboards.

---

## Phase 1 — Project Foundation
**Status:** Completed  
**Date:** 2026-11-15

### What Was Implemented
- **Solution & Build Governance:** `MultiTenantSaaS.sln`, `global.json` (`9.0.300`), `Directory.Build.props` (`net9.0`, nullable, warnings as errors, analyzers), `Directory.Packages.props` (Central Package Management), `.config/dotnet-tools.json` (`dotnet-ef` 9.0.23).
- **Domain Primitives (`SaaS.Domain`):** `Entity`, `AggregateRoot`, `TenantEntity`, `ITenantScoped`, `ValueObject`, `IDomainEvent`, `DomainException` — zero external dependencies.
- **Application Abstractions (`SaaS.Application`):** `ICommandHandler`, `IQueryHandler`, `IDateTimeProvider`, ISP-segregated `ITenantContext` / `ITenantContextSetter`, `ICurrentUser`, `IUnitOfWork`, `PagedResult<T>`, `PaginationQuery`, and `AppException` hierarchy.
- **Infrastructure (`SaaS.Infrastructure`):** `ApplicationDbContext` (`IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`) with `snake_case` naming, fail-closed `ITenantScoped` global query filter, `TenantWriteGuardInterceptor`, `EfUnitOfWork` (retrying execution strategy safe), `SystemDateTimeProvider`, `TenantContext`, `CurrentUser`, `DesignTimeDbContextFactory`, and `InitialIdentityFoundation` migration.
- **API & Cross-Cutting (`SaaS.API`):** Serilog (`AddSerilog`), `CorrelationIdMiddleware`, `SecurityHeadersMiddleware`, `ExceptionHandlingMiddleware` (RFC 9457 `ProblemDetails`), API Versioning (`/api/v1/...`), Swagger/OpenAPI with `Bearer` and `X-Tenant-Id` security definitions, Rate Limiting, CORS policy, `/health/live`, `/health/ready`, and `SystemController`.
- **DevOps & CI:** `src/SaaS.API/Dockerfile`, `docker-compose.yml`, `.env.example`, `.github/workflows/ci.yml`, `README.md`, `CONTRIBUTING.md`, `CHANGELOG.md`.

### Architectural Decisions Confirmed
- Aligned target framework to `net9.0` matching installed SDK `9.0.300` while maintaining `.NET 10` API parity ([ADR-008](../decisions/ADRs/ADR-008-dotnet-sdk-and-openapi-tooling.md)).
- Global query filter uses strict fail-closed condition (`_tenantContext.TenantId != null && e.TenantId == _tenantContext.TenantId`), returning zero rows when unresolved.
- Added `TenantWriteGuardInterceptor` to prevent cross-tenant writes during `SaveChangesAsync`.
- Avoided `Log.Logger` freeze issues in multi-factory integration tests by configuring Serilog via `services.AddSerilog(...)`.

### Tests Executed (Post Final Code Edit)
- `dotnet build MultiTenantSaaS.sln --no-restore -p:UseSharedCompilation=false -m:1`: **0 Warnings, 0 Errors**.
- `dotnet test MultiTenantSaaS.sln --no-build --verbosity normal`:
  - `SaaS.Domain.UnitTests`: **5/5 Passed**
  - `SaaS.Application.UnitTests`: **3/3 Passed**
  - `SaaS.API.IntegrationTests`: **11/11 Passed** (4 NetArchTest boundary checks, 3 `TenantContext`/`TenantWriteGuardInterceptor` tests, 4 live PostgreSQL 17 Testcontainers HTTP endpoint & migration tests)
- `docker compose --env-file .env.example config --quiet`: **Passed (exit code 0)**.

### Known Limitations (Expected at Phase 1)
- Authentication endpoints, tenant creation/resolution middleware, RBAC, subscriptions, audit logging, and frontend UI are scheduled for Phases 2–5.
- `.csproj` `Cors` origin and `Jwt` signing keys are configured via environment variables (no hardcoded dev secrets in `appsettings.json`).

### Next Milestone
- **Phase 2 — Identity and Tenant Foundation:** Implement Domain & EF Core configurations for `Tenant`, `TenantSettings`, `TenantRole`, `RolePermission`, `TenantMembership`, `UserSession`, `RefreshToken`, `AuditLog`; build Auth endpoints (register, verify, login, refresh rotation + reuse detection, logout, sessions, password reset), `TenantResolutionMiddleware`, two seeded demo tenants, and cross-tenant isolation tests.

---

## Phase 0 — Discovery and Architecture
**Status:** Completed (2026-11-15) — Documented requirements, C4 & 4+1 views, ER diagram, 6-layer security model, API conventions, ADRs 001–008, and milestone plan.
