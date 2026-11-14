# Development Workflow & Quality Standards

## 1. Git Branching & Commit Policy

- **Branches:**
  - `main`: Always buildable and passing all unit/integration tests.
  - Feature/Fix branches: `feature/<module-name>`, `fix/<issue>`, `docs/<topic>`, `chore/<task>`.
- **Conventional Commits:**
  - `feat: implement tenant registration and onboarding`
  - `fix: block demotion of last organization owner`
  - `refactor: extract tenant membership resolver from middleware`
  - `test: add cross-tenant IDOR integration tests`
  - `docs: add 4+1 architectural views and ER diagram`
  - `chore: configure postgresql docker compose and ci workflow`

---

## 2. Pre-Commit & Milestone Review Checklist

Before every commit and milestone completion:
1. Inspect `git status` and `git diff` to verify no secrets, `.env`, or unrelated modifications are staged.
2. Run `dotnet build` and relevant `dotnet test` suites (unit + PostgreSQL integration tests).
3. Perform senior engineer self-review checking:
   - Clean Architecture dependency boundaries (Domain pure, thin Controllers).
   - SOLID adherence (no god services, no duplicated logic).
   - Security boundaries (tenant isolation, authorization attributes, validation, `ProblemDetails`).
   - Documentation synchronization (`CHANGELOG.md`, `progress-report.md`, OpenAPI/ADRs).
