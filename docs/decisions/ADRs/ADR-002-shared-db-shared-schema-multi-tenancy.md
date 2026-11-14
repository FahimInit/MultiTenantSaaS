# ADR-002: Shared Database & Shared Schema Multi-Tenancy

**Status:** Accepted  
**Date:** 2026-11-15

## Context
A multi-tenant B2B SaaS platform must isolate tenant data reliably while remaining cost-efficient and manageable for schema migrations across hundreds or thousands of tenants.

## Decision
Use a **Shared Database, Shared Schema** strategy in PostgreSQL where every tenant-owned table includes a non-nullable `tenant_id` foreign key, backed by six-layer defense-in-depth:
1. Authenticated JWT + validated `X-Tenant-Id` membership middleware
2. Explicit permission policy enforcement
3. Handler-level `(id, tenantId)` queries and invariants
4. EF Core global query filters + `SaveChangesAsync` tenant write-guard interceptor
5. PostgreSQL composite foreign keys `(tenant_id, role_id)` and partial unique indexes
6. Optional PostgreSQL Row-Level Security (`SET LOCAL app.current_tenant_id`)

## Alternatives Considered
- **Database-per-Tenant:** High connection pool fragmentation, expensive migrations across N databases, excessive operational complexity for a SaaS management foundation.
- **Schema-per-Tenant:** Better isolation than shared schema, but EF Core migration orchestration and connection pooling still grow complex as tenant count scales.

## Consequences
- **Positive:** Single migration pipeline, shared PostgreSQL connection pool, straightforward cross-tenant platform analytics.
- **Negative:** Higher blast radius if isolation is misconfigured; mitigated by multi-layer checks, composite FKs, and automated cross-tenant integration tests.
