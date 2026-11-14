# System Overview

## 1. Architectural Vision

The **Multi-Tenant SaaS Management Platform** is designed as a **Clean Architecture Modular Monolith** written in C# / ASP.NET Core paired with a Next.js TypeScript administration frontend and PostgreSQL primary persistence.

Rather than introducing premature distributed systems complexity (microservices, separate databases per module, service meshes), the system establishes strict **compile-time project boundaries**, **logical module isolation**, and **database-enforced tenant boundaries** inside a single cohesive deployment unit.

---

## 2. Primary Architectural Drivers

1. **Bulletproof Tenant Isolation (Highest Priority):** Shared-database, shared-schema multi-tenancy demands defense-in-depth so no single oversight (such as forgetting a query filter or trusting a client header) can cause cross-tenant data leakage.
2. **Clear Module Ownership (Modular Monolith):** Identity, Tenant Management, Memberships, RBAC, Subscriptions, Audit Logging, and Dashboards each operate with well-defined domain invariants and application use cases.
3. **Production Realism without Overengineering:** Real PostgreSQL persistence, ACID transactions, optimistic concurrency, token rotation/reuse detection, rate limiting, and RFC 9457 `ProblemDetails` — while deferring Redis and background worker infrastructure until Phase 4 when justified.
4. **High Testability:** Pure Domain layer with zero external dependencies, deterministic Application handlers, and real PostgreSQL integration tests via Docker.

---

## 3. High-Level Architectural Summary

| Concern | Chosen Approach | Primary Trade-off |
|---|---|---|
| **Architecture Style** | Clean Architecture + Modular Monolith | Single deployable binary simplifies transactions and refactoring; horizontal scaling relies on stateless API replicas rather than independent per-module scaling. |
| **Multi-Tenancy Model** | Shared Database, Shared Schema (`TenantId`) | Maximizes resource efficiency and operational simplicity; requires rigorous defense-in-depth (middleware + handler validation + EF filters + composite FKs). |
| **Tenant Resolution** | Validated `X-Tenant-Id` header + Membership check | Enables instant multi-org switching and multi-tab workflows without re-minting JWTs; requires fast membership lookup on tenant-scoped requests. |
| **CQRS Dispatch** | Explicit `ICommandHandler` / `IQueryHandler` interfaces | Zero external mediator dependency/licensing overhead and explicit call graphs; requires straightforward DI registration per handler. |
| **RBAC Model** | Per-tenant seeded system roles + custom roles | Eliminates nullable `TenantId` special cases in roles and enables composite FKs `(TenantId, RoleId)`; requires syncing new permissions across existing tenants via migrations. |
| **Frontend Integration** | Next.js App Router + TanStack Query + Zod + shadcn/ui | Clean separation of concerns; API remains a standalone REST contract usable by any client. |

---

## 4. Document Map

- [Clean Architecture & Dependency Rules](./clean-architecture.md)
- [C4 System Context & Container Diagrams](./diagrams/c4-context-and-container.md)
- [4+1 Logical View](./logical-view.md)
- [4+1 Development View](./development-view.md)
- [4+1 Process View](./process-view.md)
- [4+1 Physical View](./physical-view.md)
- [4+1 Scenario View](./scenario-view.md)
- [Database Design](../database/database-design.md) & [ER Diagram](../database/er-diagram.md)
- [Multi-Tenancy Security Model](../security/security-model.md)
- [API Conventions & Endpoint Catalog](../api/api-conventions.md)
- [Architecture Decision Records](../decisions/ADRs/)
