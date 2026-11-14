# ADR-005: Per-Tenant Seeded System Roles and Custom Roles

**Status:** Accepted  
**Date:** 2026-11-15

## Context
The platform requires initial organization roles (`Organization Owner`, `Organization Administrator`, `Manager`, `Member`) plus custom organization roles on paid plans, while strictly preventing cross-tenant role references.

## Decision
Every `TenantRole` row belongs to a specific `TenantId` (no nullable `TenantId` shared template rows in `tenant_roles`). When a tenant is created, a role template factory seeds the 4 system roles (`ORG_OWNER`, `ORG_ADMIN`, `MANAGER`, `MEMBER`, marked `IsSystemRole = true`) along with their capability mappings in `tenant_role_permissions`. Platform Administrator (`PlatformAdministrator`) is modeled separately at the user/platform level.

## Alternatives Considered
- **Global system roles with `TenantId = NULL`:** Breaks strict non-null `tenant_id` invariant on tenant tables and prevents composite FK `(tenant_id, role_id) -> tenant_roles(tenant_id, id)` on `tenant_memberships` and `tenant_invitations`.

## Consequences
- **Positive:** Uniform schema, composite FK enforcement in PostgreSQL, clean support for future per-tenant role customization without schema hacks.
- **Negative:** Adding a brand-new permission capability later requires a deterministic migration/sync script to backfill existing tenants' system roles (straightforward via stable `Code` identifiers).
