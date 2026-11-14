# Multi-Tenant SaaS Management Platform

A production-oriented B2B **Multi-Tenant SaaS Management Platform** engineered with **Clean Architecture**, **Modular Monolith** boundaries, **ASP.NET Core**, **Entity Framework Core**, **PostgreSQL**, and **Next.js (TypeScript)**.

> **Current Milestone:** Phase 1 — Project Foundation (Completed) | Next: Phase 2 — Identity & Multi-Tenant Foundation

---

## Architecture & Documentation

- [System Overview](docs/architecture/system-overview.md)
- [Clean Architecture & SOLID Boundaries](docs/architecture/clean-architecture.md)
- [C4 System Context & Container Diagrams](docs/architecture/diagrams/c4-context-and-container.md)
- [4+1 View Model](docs/architecture/logical-view.md) (`logical`, `development`, `process`, `physical`, `scenario`)
- [Database ER Diagram](docs/database/er-diagram.md) & [PostgreSQL Design](docs/database/database-design.md)
- [Six-Layer Multi-Tenancy Security Model](docs/security/security-model.md)
- [API Conventions & Endpoint Catalog](docs/api/api-conventions.md)
- [Architecture Decision Records](docs/decisions/ADRs/)
- [Milestone Plan & Progress Report](docs/development/milestone-plan.md)

---

## Solution Layout

```text
src/
├── SaaS.Domain/          # Pure C# domain model (zero external dependencies)
├── SaaS.Application/     # Use cases, CQRS handlers, FluentValidation, ports
├── SaaS.Infrastructure/  # EF Core, PostgreSQL, Identity, Serilog, integrations
└── SaaS.API/             # Composition root, REST controllers, middleware, OpenAPI

tests/
├── SaaS.Domain.UnitTests/
├── SaaS.Application.UnitTests/
└── SaaS.API.IntegrationTests/
```

---

## Local Development

1. Copy environment template:
   ```bash
   cp .env.example .env
   ```
2. Start PostgreSQL (and API container if desired):
   ```bash
   docker compose up -d postgres
   ```
3. Build and test the backend:
   ```bash
   dotnet build
   dotnet test
   ```
4. Run the API locally:
   ```bash
   dotnet run --project src/SaaS.API
   ```
   OpenAPI / Swagger UI (Development): `http://localhost:8080/swagger`  
   Health Checks: `/health/live`, `/health/ready`
