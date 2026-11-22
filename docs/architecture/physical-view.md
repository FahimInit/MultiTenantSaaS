# 4+1 Physical View

The Physical View maps software containers onto infrastructure nodes, Docker services, network boundaries, and CI/CD pipelines.

---

## 1. Container & Deployment Topology

```mermaid
flowchart TB
    Client["Client Browser<br/>(HTTPS)"]

    subgraph DockerHost["Docker Compose Network (saas-network)"]
        subgraph PublicEdge["Frontend & API Containers"]
            Web["saas-frontend<br/>Next.js 15/16 (Node Runtime)<br/>Port 3000"]
            Api["saas-api<br/>ASP.NET Core API<br/>Port 8080"]
        end

        subgraph DataTier["Internal Data Network (Not Exposed in Prod)"]
            Pg[("saas-postgres<br/>PostgreSQL 17-alpine<br/>Port 5432 + Named Volume pgdata")]
            Redis[("saas-redis (Phase 4)<br/>Redis 7-alpine<br/>Port 6379")]
        end
    end

    GitHub["GitHub Actions CI/CD<br/>(Build, Unit Tests, Testcontainers Integration Tests,<br/>Next.js Typecheck/Build, Docker Image Validation)"]

    Client -->|"HTTP/HTTPS :3000"| Web
    Web -->|"REST API :8080"| Api
    Client -->|"Direct API / Swagger (Dev) :8080"| Api
    Api -->|"TCP :5432"| Pg
    Api -.->|"Phase 4 TCP :6379"| Redis
    GitHub -.->|"Builds & Verifies Containers"| DockerHost
```

---

## 2. Live Cloud Showcase Topology

For public portfolio hosting, each tier runs on managed cloud infrastructure over TLS:

```mermaid
flowchart LR
    Visitor["Browser Client<br/>(HTTPS)"] -->|"Static & App Router UI"| Vercel["Vercel<br/>Next.js 15 Frontend<br/>(multi-tenant-saa-s-seven.vercel.app)"]
    Visitor -->|"CORS-Allowed REST + JWT + X-Tenant-Id"| Render["Render Web Service<br/>Docker Container (src/SaaS.API/Dockerfile)"]
    Render -->|"Npgsql TLS (SSL Mode=Require)"| Neon[("Neon Serverless PostgreSQL<br/>Managed Cloud Database")]
```

---

## 3. Environment & Secret Management Strategy

- **Zero Secrets in Repository:** `.env.example` provides structural templates only; actual secrets (`JWT_SIGNING_KEY`, `POSTGRES_PASSWORD`, `SMTP_PASSWORD`) are injected via environment variables or Docker secrets.
- **Database Credentials Isolation:** Application connects with a dedicated least-privilege PostgreSQL user (not cluster superuser in production) and executes migrations cleanly via startup migration runner (dev) or CI migration bundle/command (prod).
- **Health Checks & Readiness:**
  - `saas-postgres` defines `pg_isready` health check.
  - `saas-api` exposes `/health/live` (liveness) and `/health/ready` (verifying PostgreSQL + Redis connectivity).
  - `saas-frontend` depends on healthy `saas-api` in Docker Compose.

---

## 4. CI/CD Pipeline Flow (GitHub Actions)

```mermaid
flowchart LR
    Trigger["Push / Pull Request<br/>(.github/workflows/ci.yml)"] --> BackendJob["Backend Job<br/>Restore & Build .NET 9 Release"]
    Trigger --> FrontendJob["Frontend Job<br/>npm ci + tsc --noEmit"]

    BackendJob --> UnitTests["Domain & Application<br/>Unit Tests (xUnit)"]
    UnitTests --> IntTests["API & PostgreSQL Integration Tests<br/>(Testcontainers + NetArchTest)"]

    FrontendJob --> Lint["ESLint Check<br/>(npm run lint)"]
    Lint --> FeBuild["Next.js Production Build<br/>(npm run build)"]
```

1. **Trigger:** Push or Pull Request to `main` / `develop` (or `master`).
2. **Backend Job:**
   - Restore & Build (`dotnet build --configuration Release`).
   - Run `SaaS.Domain.UnitTests` and `SaaS.Application.UnitTests`.
   - Run `SaaS.API.IntegrationTests` against PostgreSQL (via Testcontainers or GitHub Actions `services: postgres:17`).
3. **Frontend Job:**
   - Install dependencies (`npm ci`), type-check (`tsc --noEmit`), lint (`npm run lint`), and production build (`npm run build`).
4. **Container Verification (Phase 6):**
   - Build Docker images (`Dockerfile` for API and Frontend) and verify container startup & health checks.
