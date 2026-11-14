# Developer Setup Guide

## Prerequisites

- **.NET 9 SDK (`9.0.300` or compatible, see `global.json` and [ADR-008](../decisions/ADRs/ADR-008-dotnet-sdk-and-openapi-tooling.md))**
- **Node.js `24.x` / `npm 11.x`** (for Phase 5 Frontend)
- **Docker Desktop / Docker Engine** (required for PostgreSQL container & Testcontainers integration tests)
- **Git `2.46+`**

## Local Setup

1. **Copy environment template:**
   ```bash
   cp .env.example .env
   ```
   Update `POSTGRES_PASSWORD` and `Jwt__SigningKey` (32+ chars) in `.env`.

2. **Restore local EF Core CLI tool & NuGet packages:**
   ```bash
   dotnet tool restore
   dotnet restore
   ```

3. **Start PostgreSQL via Docker Compose:**
   ```bash
   docker compose up -d postgres
   ```

4. **Apply EF Core migrations:**
   ```bash
   dotnet tool run dotnet-ef database update --project src/SaaS.Infrastructure --startup-project src/SaaS.API
   ```

5. **Run all tests (Unit + Architecture + PostgreSQL Testcontainers Integration):**
   ```bash
   dotnet test
   ```

6. **Run the API:**
   ```bash
   dotnet run --project src/SaaS.API
   ```
   - Swagger UI (Development): `http://localhost:5025/swagger` (or `:8080` in Docker)
   - Liveness: `/health/live`
   - Readiness (PostgreSQL): `/health/ready`
