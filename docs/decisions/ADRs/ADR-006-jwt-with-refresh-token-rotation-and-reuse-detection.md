# ADR-006: Short-Lived JWT Access Tokens with Refresh Token Rotation & Reuse Detection

**Status:** Accepted  
**Date:** 2026-11-15

## Context
The platform serves a Next.js SPA/SSR client and REST API consumers requiring stateless request authentication alongside revocable sessions and protection against refresh token theft.

## Decision
Issue short-lived JWT access tokens (15 minutes) paired with opaque cryptographic refresh tokens persisted in PostgreSQL (`refresh_tokens`) as **SHA-256 hashes** grouped by `token_family_id` and `session_id`:
- Every `/auth/refresh` call atomically revokes the presented token (`replaced_by_token_id`) and issues a fresh pair in the same family.
- Presenting an already-revoked token triggers **reuse detection**, revoking the entire `token_family_id` and `user_sessions` record, and logging a security audit event.

## Alternatives Considered
- **Long-lived JWTs without refresh tokens:** Impossible to revoke compromised tokens promptly without checking a store on every request.
- **Unrotated refresh tokens:** A stolen token remains valid until manual logout or full expiry.

## Consequences
- **Positive:** Immediate breach containment on token replay, granular per-device session revocation, no plaintext secrets stored in the database.
- **Negative:** Concurrent tab refresh races in the frontend must be serialized (handled cleanly by a single refresh promise lock inside the frontend API client).
