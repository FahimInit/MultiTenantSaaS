# ADR-003: Header-Based Tenant Resolution Validated Against Active Membership

**Status:** Accepted  
**Date:** 2026-11-15

## Context
Users can belong to multiple organizations (e.g., an agency consultant or user invited to two companies) and switch workspaces smoothly in the Next.js UI or open two organizations in separate browser tabs.

## Decision
Resolve the active tenant via the HTTP header `X-Tenant-Id: <uuid>` on tenant-scoped endpoints, **never trusting it blindly**: `TenantResolutionMiddleware` queries `TenantMemberships` for `(AuthenticatedUserId, X-Tenant-Id)`, verifies `MembershipStatus == Active` and `TenantStatus` is operational, and populates a scoped `ITenantContext`. Unauthorized attempts log `CROSS_TENANT_ACCESS_DENIED` and return `403 Forbidden`.

## Alternatives Considered
- **Encoding a single `tenant_id` inside the JWT:** Requires re-issuing JWTs every time a user switches organizations and breaks multi-tab workflows where Tab 1 views Org A and Tab 2 views Org B.
- **Subdomain-based resolution (`org-a.saas.com`):** Adds wildcard DNS/TLS and cookie-domain complexity in local Docker/CI environments.
- **Route parameter (`/api/v1/tenants/{tenantId}/...`):** Verbose on every endpoint (`members`, `roles`, `audit-logs`, `subscriptions`); header resolution keeps resource URLs clean while remaining explicit.

## Consequences
- **Positive:** Clean REST URLs, seamless multi-org switching, multi-tab isolation, instant permission/membership revocation detection.
- **Negative:** Requires a fast `(tenant_id, user_id)` lookup per tenant-scoped request (indexed unique key in PostgreSQL; optionally cached in Redis in Phase 4 with explicit invalidation).
