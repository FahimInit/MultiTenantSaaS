# ADR-008: Target .NET SDK Alignment (`net9.0`) & OpenAPI/Swagger Tooling

**Status:** Accepted (Phase 1 Baseline)  
**Date:** 2026-11-15

## Context
1. **SDK Version Finding:** The project specification lists `.NET 10 LTS` and `EF Core 10`, while inspection of the local workstation (`dotnet --info`) verified **`.NET SDK 9.0.300`** installed locally (with `.NET 8` and `.NET 9` runtimes; `.NET 10` SDK is not installed).
2. **Swagger / OpenAPI in .NET 9:** `Swashbuckle.AspNetCore` v7+/v8+ or `Microsoft.AspNetCore.OpenApi` + `Swashbuckle.AspNetCore.SwaggerUI` provide OpenAPI 3.0/3.1 schema generation and interactive Swagger UI (`/swagger`).

## Decision
1. **Target Framework (`net9.0`):** Target `net9.0` across all backend projects and Docker images (`mcr.microsoft.com/dotnet/sdk:9.0` / `aspnet:9.0`) using compatible **EF Core 9 (`Npgsql.EntityFrameworkCore.PostgreSQL` 9.x)** packages so local CLI (`dotnet build`, `dotnet test`, `dotnet ef`), Docker Compose, and GitHub Actions CI run on the exact same verified SDK (`9.0.300`). Avoid deprecated APIs so upgrading `<TargetFramework>net9.0</TargetFramework>` to `net10.0` remains a trivial one-line change once `.NET 10` SDK is installed.
2. **OpenAPI / Swagger:** Configure ASP.NET Core API Versioning (`Asp.Versioning.Mvc` + `Asp.Versioning.Mvc.ApiExplorer`) paired with `Swashbuckle.AspNetCore` (verified compatible with .NET 9) so `/swagger` and `/swagger/v1/swagger.json` work cleanly with JWT Bearer and `X-Tenant-Id` security schemes.

## Consequences
- **Positive:** 100% deterministic local, Docker, and CI builds without requiring global system SDK changes or unverified package mismatches.
- **Negative:** Targets .NET 9 STS rather than .NET 10 LTS until the host/CI SDK is bumped to `net10.0`.
