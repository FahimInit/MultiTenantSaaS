# Contributing Guidelines

Thank you for contributing to the Multi-Tenant SaaS Management Platform. Please follow the project's architectural and engineering standards outlined in [Development Workflow](docs/development/development-workflow.md).

## Architectural Rules

1. **Dependency Direction:** `API -> Application -> Domain` and `Infrastructure -> Application -> Domain`. Never add infrastructure or framework dependencies inside `SaaS.Domain`.
2. **Thin Controllers:** Controllers validate transport boundaries and delegate immediately to Application command/query handlers. Never inject `ApplicationDbContext` directly into a controller.
3. **Tenant Isolation:** Never trust client-supplied `TenantId` headers or payload IDs without verified membership and explicit tenant-scoped queries.
4. **No Raw Entities Over HTTP:** Always map to explicit Request/Response DTOs.
5. **No Secrets in Git:** Keep local secrets out of source control; update `.env.example` when introducing new configuration keys.

## Commit & Branch Conventions

Use **Conventional Commits**:
- `feat: ...`
- `fix: ...`
- `refactor: ...`
- `test: ...`
- `docs: ...`
- `chore: ...`

Run `dotnet build` and `dotnet test` before committing any changes.
