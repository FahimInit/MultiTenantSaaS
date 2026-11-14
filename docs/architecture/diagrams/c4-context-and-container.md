# C4 System Context & Container Diagrams

## 1. C4 Level 1 — System Context Diagram

Shows how human actors and external systems interact with the Multi-Tenant SaaS Management Platform.

```mermaid
flowchart TB
    OrgUser["Organization Member / Manager / Admin / Owner<br/>(Browser User)"]
    PlatAdmin["Platform Administrator<br/>(SaaS Operator)"]
    
    Platform["Multi-Tenant SaaS Management Platform<br/>(Next.js Web App + ASP.NET Core Modular Monolith)"]
    
    EmailSys["Email Delivery Service<br/>(SMTP / Dev Sink)"]
    PaySys["Payment Provider<br/>(MockPaymentProvider / Future Stripe)"]

    OrgUser -->|"Registers, switches orgs, manages members,<br/>roles, subscriptions, views audit logs (HTTPS)"| Platform
    PlatAdmin -->|"Manages subscription plans, monitors platform metrics,<br/>suspends/reactivates tenants (HTTPS)"| Platform
    Platform -->|"Sends verification, password reset,<br/>and invitation emails (SMTP)"| EmailSys
    Platform -->|"Processes subscription upgrades,<br/>downgrades, and cancellations (IPaymentProvider)"| PaySys
```

---

## 2. C4 Level 2 — Container Diagram

Illustrates the runtime containers and communication protocols inside the platform boundary.

```mermaid
flowchart TB
    Browser["Web Browser<br/>(End Users & Platform Admins)"]

    subgraph PlatformBoundary["Multi-Tenant SaaS Platform (Docker Compose / Host)"]
        WebApp["Next.js Web Application<br/>(TypeScript, React, Tailwind, shadcn/ui,<br/>TanStack Query, React Hook Form, Zod)"]
        ApiApp["ASP.NET Core Web API<br/>(Modular Monolith: Identity, Tenants, Members,<br/>RBAC, Subscriptions, Audit, Dashboard)"]
        Postgres[("PostgreSQL Database<br/>(Shared DB, Shared Schema with TenantId,<br/>Composite FKs & Optional RLS)")]
        Redis[("Redis Cache<br/>(Phase 4: Tenant Membership Cache,<br/>Distributed Rate Limiting)")]
        BgWorker["In-Process Background Workers<br/>(Phase 4: IHostedService for Trial/Invite Expiry,<br/>Audit Retention & Usage Snapshots)"]
    end

    EmailExt["SMTP / Dev Email Sink"]
    PayExt["Mock / External Payment Gateway"]

    Browser -->|"HTTPS / UI"| WebApp
    WebApp -->|"REST JSON + JWT Bearer + X-Tenant-Id (HTTPS)"| ApiApp
    ApiApp -->|"EF Core / Npgsql (TCP 5432)"| Postgres
    ApiApp -.->|"Phase 4: StackExchange.Redis (TCP 6379)"| Redis
    BgWorker -->|"Scoped DbContext / Tenant Context"| Postgres
    ApiApp -->|"IEmailService"| EmailExt
    ApiApp -->|"IPaymentProvider"| PayExt
```

### Container Responsibilities & Boundaries

- **Next.js Web Application (`frontend/`):** Minimal, responsive B2B administration dashboard. Handles user forms (Zod validation), JWT storage/refresh orchestration, active `X-Tenant-Id` switching, and rendering backend-driven metrics. Never trusted as an authorization boundary.
- **ASP.NET Core Web API (`src/SaaS.API` + `Application` + `Domain` + `Infrastructure`):** Enforces all authentication, tenant resolution, permission authorization, quota checks, domain invariants, ACID transactions, and audit trails.
- **PostgreSQL:** Primary source of truth for identity, tenants, RBAC, subscriptions, usage snapshots, and immutable audit logs.
- **Redis (Phase 4):** Introduced only after core correctness is established — caches resolved `(UserId, TenantId)` memberships/permissions with explicit invalidation on membership/role updates, and backs distributed rate limiting.
