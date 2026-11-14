# ADR-001: Modular Monolith with Clean Architecture

**Status:** Accepted  
**Date:** 2026-11-15

## Context
The platform requires clear separation across seven cohesive business modules (Identity, Tenants, Memberships, RBAC, Subscriptions, Audit, Dashboard) while maintaining strong maintainability, security, and testability without premature microservices overhead.

## Decision
Adopt a **Modular Monolith** organized inside four Clean Architecture projects (`SaaS.Domain`, `SaaS.Application`, `SaaS.Infrastructure`, `SaaS.API`), partitioned internally by business module folders. `SaaS.Domain` remains 100% dependency-free.

## Alternatives Considered
- **Microservices:** Rejected due to distributed transaction complexity (creating a tenant + owner + roles + subscription + audit log atomically), operational overhead, and explicit spec guidance against premature microservices.
- **N-Layer / Anemic CRUD:** Rejected because putting business invariants in services/controllers violates SOLID and weakens domain unit testing.

## Consequences
- **Positive:** Single deployment artifact, ACID database transactions across modules, fast local debugging, compile-time layer enforcement, easy future extraction if a module ever justifies scaling independently.
- **Negative:** Developers must maintain discipline around module boundaries (mitigated via NetArchTest architecture rules in integration tests).
