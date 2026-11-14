# ADR-004: Explicit Application Handlers Without MediatR

**Status:** Accepted  
**Date:** 2026-11-15

## Context
Clean Architecture use cases benefit from command/query separation (CQRS), clear input validation, and single-responsibility handlers. Historically, .NET teams often pull in `MediatR`, though recent licensing shifts (MediatR v13+ commercial licensing) and indirection trade-offs warrant deliberate evaluation.

## Decision
Define lightweight, first-party interfaces in `SaaS.Application`:
- `ICommandHandler<TCommand, TResponse>` / `ICommandHandler<TCommand>`
- `IQueryHandler<TQuery, TResponse>`
- Explicit `IValidator<T>` validation inside a transparent command/handler pipeline or lightweight `IApplicationDispatcher` (under 60 lines of internal code) with zero third-party mediator dependency.

## Alternatives Considered
- **MediatR:** Introduces reflection-based runtime dispatch, harder stack traces, and licensing constraints on newer versions.
- **Fat Application Services (`TenantService` with 15 methods):** Tends toward god classes violating SRP.

## Consequences
- **Positive:** Zero licensing risk, explicit compile-time dependencies, clean stack traces, strict SRP (one handler class per use case), adheres to "avoid unnecessary abstraction and dependencies."
- **Negative:** Slightly more explicit constructor/method wiring in thin controllers or registration via assembly scanning extension.
