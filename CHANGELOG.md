# Changelog

All notable changes to this project are documented in this file following [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and Conventional Commits.

## [Unreleased]

### Phase 2 — Identity & Multi-Tenant Foundation
- **Domain Model (`SaaS.Domain`):** Added `Tenant`, `TenantSlug` (normalized + reserved slug protection), `TenantSettings`, `TenantRole`, `RolePermission`, `PermissionCatalog` (`SystemRoleCodes` + 14 tenant permissions), `TenantMembership`, `UserSession`, `RefreshToken`, and `AuditLog` (`AuditScope`, `AuditCategory`).
- **PostgreSQL Schema (`SaaS.Infrastructure`):** Added explicit EF Core configurations, `AddIdentityAndTenantFoundation` migration, composite alternate key `(tenant_id, id)` on `tenant_roles`, composite FK `(tenant_id, role_id)` on `tenant_memberships`, `xmin` optimistic concurrency tokens, `jsonb` audit metadata, fail-closed `AuditLog` global query filter, and `DatabaseSeeder` (Platform Admin, Acme Corp, Globex Labs, and multi-tenant user).
- **Identity & Tenant Modules (`SaaS.Application` / `SaaS.Infrastructure` / `SaaS.API`):** Implemented registration, email verification (`DevelopmentEmailService` outbox), login (`403 email_not_verified` enforcement), SHA-256 refresh token rotation with token-family reuse detection & session revocation, logout, password reset, session listing/revocation, profile query, organization creation (`POST /api/v1/tenants`), membership listing, tenant updates, settings updates, and permission-protected `[HasPermission]` / `[PlatformAdminOnly]` / `[UserScoped]` attributes.
- **Security & Isolation Middleware (`SaaS.API`):** Implemented `TenantResolutionMiddleware` validating `X-Tenant-Id` against active DB memberships and auditing denied cross-tenant attempts, plus `OnTokenValidated` active session verification.
- **Tests:** Added domain unit tests, application handler unit tests, and live PostgreSQL Testcontainers integration tests covering auth flows, token reuse revocation, email verification, and cross-tenant isolation (**46/46 passing** across all 3 test projects).

### Phase 1 — Project Foundation
- Initialized Git repository (`main`), `MultiTenantSaaS.sln`, `Directory.Build.props`, `Directory.Packages.props` (Central Package Management), and `.config/dotnet-tools.json` (`dotnet-ef` 9.0.23).
- Created Clean Architecture projects (`SaaS.Domain`, `SaaS.Application`, `SaaS.Infrastructure`, `SaaS.API`) and test suites (`SaaS.Domain.UnitTests`, `SaaS.Application.UnitTests`, `SaaS.API.IntegrationTests`).
- Implemented Domain base primitives (`Entity`, `AggregateRoot`, `TenantEntity`, `ITenantScoped`, `ValueObject`, `DomainException`), Application CQRS/pagination/exception contracts, and fail-closed `TenantContext` + `TenantWriteGuardInterceptor`.
- Configured PostgreSQL + EF Core 9 (`Npgsql`, `EFCore.NamingConventions`), `ApplicationDbContext` with initial `InitialIdentityFoundation` migration, ASP.NET Identity Core foundation, Serilog structured logging, API versioning, Swagger/OpenAPI, Rate Limiting, Security Headers, Correlation ID, and RFC 9457 `ProblemDetails`.
- Configured `Dockerfile`, `docker-compose.yml`, `.env.example`, and GitHub Actions CI workflow (`.github/workflows/ci.yml`).
- Added unit tests, NetArchTest architecture boundary tests, and Testcontainers PostgreSQL integration tests (19/19 passing).

### Phase 0 — Discovery & Architecture
- Added complete functional and non-functional requirements specification.
- Added Clean Architecture & Modular Monolith guidelines and C4 context/container diagrams.
- Added 4+1 Architectural View Model (`logical`, `development`, `process`, `physical`, `scenario`).
- Designed PostgreSQL schema, ER diagram, composite foreign keys, and partial unique indexes.
- Documented 6-layer multi-tenancy security model, API conventions, milestone plan, and initial ADRs (`ADR-001` through `ADR-008`).
