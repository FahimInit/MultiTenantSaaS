# ADR-007: Deferred Redis Caching and Mock Payment Provider Abstraction

**Status:** Accepted  
**Date:** 2026-11-15

## Context
Both Redis and payment integration can distract from core domain correctness, transactional integrity, and multi-tenant isolation if introduced prematurely.

## Decision
1. **Redis (Deferred to Phase 4):** Phases 1–3 rely on PostgreSQL indexing and lightweight in-process abstractions (`ICacheService` / `IMemoryCache` where needed). Redis is introduced in Phase 4 specifically for distributed membership/permission resolution caching (with explicit invalidation) and distributed rate limiting.
2. **Payment Provider Abstraction (`IPaymentProvider`):** Subscription business logic, plan quotas, trial lifecycles, and immutable `SubscriptionHistory` are implemented completely in Phase 3 against `IPaymentProvider`, backed initially by a deterministic `MockPaymentProvider`.

## Consequences
- **Positive:** Early milestones focus 100% on database integrity, security boundaries, and domain behavior; swapping in Stripe or Redis later requires zero business logic rewrites.
- **Negative:** Multi-instance horizontal scaling of rate limits and membership caches waits until Phase 4 Redis integration.
