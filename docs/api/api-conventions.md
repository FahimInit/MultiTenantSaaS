# API Conventions & Module Endpoint Breakdown

## 1. REST & HTTP Standards

- **Base Path & Versioning:** URL segment versioning starting at `/api/v1/...`.
- **Content Negotiation:** `application/json` for requests/responses; `application/problem+json` for errors.
- **Tenant Header Contract:**
  - Tenant-scoped endpoints require header `X-Tenant-Id: <uuid>`.
  - User-scoped (`/auth/*`, `/tenants` list/create, `/invitations/accept`) and platform-admin endpoints (`/platform/*`) ignore or reject unauthorized `X-Tenant-Id` usage cleanly.
- **DTO Discipline:** Controllers accept explicit `*Request` DTOs and return explicit `*Response` / `*Dto` records. EF Core and Domain entities are **never** serialized directly.
- **CancellationToken:** Every async controller action and handler forwards `CancellationToken` down to EF Core and external ports.

---

## 2. Error Handling Contract (RFC 9457 `ProblemDetails`)

All non-2xx errors return structured `ProblemDetails` with `correlationId` / `traceId` extension fields:

| Exception / Condition | HTTP Status | Error Type Code |
|---|---|---|
| FluentValidation / invalid input | `400 Bad Request` | `validation_error` (with `errors` dictionary) |
| Missing/expired/invalid JWT or token reuse | `401 Unauthorized` | `unauthorized` |
| Missing tenant membership, missing permission, suspended tenant | `403 Forbidden` | `forbidden` / `tenant_access_denied` / `tenant_suspended` |
| Resource ID not found (including cross-tenant IDOR!) | `404 Not Found` | `resource_not_found` |
| Duplicate slug, duplicate invite, optimistic concurrency (`xmin`) | `409 Conflict` | `conflict` |
| Plan limit / feature restriction exceeded | `402 Payment Required` or `403 Forbidden` (`quota_exceeded`) | `quota_exceeded` |
| Rate limit exceeded | `429 Too Many Requests` | `rate_limit_exceeded` |
| Unhandled server exception | `500 Internal Server Error` | `internal_error` (no internal details exposed) |

---

## 3. Pagination, Sorting & Filtering Contract

Collection endpoints accept standardized query parameters:
- `page` (default `1`, min `1`)
- `pageSize` (default `20`, min `1`, max `100`)
- `sortBy` (whitelisted field name per endpoint, e.g., `createdAt`, `name`, `occurredAt`)
- `sortDirection` (`asc` | `desc`, default `desc`)
- `search` (optional trimmed substring filter) + endpoint-specific filters (`status`, `category`, `fromUtc`, `toUtc`).

Standard response envelope:
```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 142,
  "totalPages": 8,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

---

## 4. API Module Breakdown

### Module A — Identity & Auth (`/api/v1/auth`)
- `POST /api/v1/auth/register` — Register new user account (`201`)
- `POST /api/v1/auth/verify-email` — Confirm email verification token (`200`)
- `POST /api/v1/auth/resend-verification` — Resend verification token (`202`)
- `POST /api/v1/auth/login` — Authenticate and issue JWT + refresh token (`200`)
- `POST /api/v1/auth/refresh` — Rotate refresh token (`200`)
- `POST /api/v1/auth/logout` — Revoke current session & refresh family (`204`)
- `POST /api/v1/auth/forgot-password` — Request password reset (`202`, non-enumerating response)
- `POST /api/v1/auth/reset-password` — Complete password reset & revoke sessions (`200`)
- `GET /api/v1/auth/me` — Current authenticated user profile (`200`)
- `GET /api/v1/auth/sessions` — List active user sessions (`200`)
- `DELETE /api/v1/auth/sessions/{sessionId}` — Revoke specific session (`204`)

### Module B — Tenant Management (`/api/v1/tenants`)
- `GET /api/v1/tenants` — List organizations where current user is an active member (`200`)
- `POST /api/v1/tenants` — Create and onboard a new organization (`201`)
- `GET /api/v1/tenants/current` — Get current `X-Tenant-Id` tenant details (`200`, `tenant.read`)
- `PUT /api/v1/tenants/current` — Update tenant name/slug (`200`, `tenant.update`)
- `POST /api/v1/tenants/current/complete-onboarding` — Mark onboarding completed (`200`, `tenant.update`)
- `GET /api/v1/tenants/current/settings` — Get organization settings (`200`, `tenant.settings.read`)
- `PUT /api/v1/tenants/current/settings` — Update organization settings (`200`, `tenant.settings.update`)

### Module C — Memberships & Invitations (`/api/v1/members`, `/api/v1/invitations`)
- `GET /api/v1/members` — List paginated members (`200`, `members.read`)
- `PUT /api/v1/members/{memberId}/role` — Change member's role (`200`, `members.update`)
- `PUT /api/v1/members/{memberId}/status` — Activate/suspend member (`200`, `members.update`)
- `DELETE /api/v1/members/{memberId}` — Remove member from organization (`204`, `members.remove`)
- `GET /api/v1/invitations` — List tenant invitations (`200`, `members.read`)
- `POST /api/v1/invitations` — Invite member via email + role (`201`, `members.invite`)
- `POST /api/v1/invitations/{invitationId}/resend` — Resend pending invitation (`202`, `members.invite`)
- `DELETE /api/v1/invitations/{invitationId}` — Revoke pending invitation (`204`, `members.invite`)
- `POST /api/v1/invitations/accept` — Accept invitation token (User-scoped, `200`)

### Module D — Roles & Permissions (`/api/v1/roles`, `/api/v1/permissions`)
- `GET /api/v1/permissions` — List full capability catalog (`200`, `roles.read`)
- `GET /api/v1/roles` — List system & custom roles for current tenant (`200`, `roles.read`)
- `POST /api/v1/roles` — Create custom role (checks `AllowCustomRoles` plan gate, `201`, `roles.manage`)
- `PUT /api/v1/roles/{roleId}` — Update custom role metadata & permissions (`200`, `roles.manage`)
- `DELETE /api/v1/roles/{roleId}` — Delete unreferenced custom role (`204`, `roles.manage`)

### Module E — Subscriptions & Usage (`/api/v1/subscriptions`)
- `GET /api/v1/subscriptions/plans` — List public available subscription plans (`200`)
- `GET /api/v1/subscriptions/current` — Current tenant subscription + usage summary (`200`, `billing.read`)
- `POST /api/v1/subscriptions/change-plan` — Upgrade/downgrade plan via `IPaymentProvider` (`200`, `billing.manage`)
- `POST /api/v1/subscriptions/cancel` — Cancel auto-renewal (`200`, `billing.manage`)
- `GET /api/v1/subscriptions/history` — Paginated billing/plan change history (`200`, `billing.read`)

### Module F — Audit Logs (`/api/v1/audit-logs`)
- `GET /api/v1/audit-logs` — Paginated, filterable tenant audit trail (`200`, `audit.read`)

### Module G — Dashboard (`/api/v1/dashboard`)
- `GET /api/v1/dashboard` — Tenant dashboard overview, member stats, subscription status, usage utilization, recent activities (`200`, `dashboard.read`)

### Platform Admin (`/api/v1/platform`) — `[PlatformAdminOnly]`
- `GET /api/v1/platform/dashboard` — Platform-wide KPIs & MRR summary (`200`)
- `GET /api/v1/platform/tenants` — Search/paginate all tenants (`200`)
- `GET /api/v1/platform/tenants/{tenantId}` — Inspect tenant subscription & usage metadata (`200`)
- `POST /api/v1/platform/tenants/{tenantId}/suspend` — Suspend tenant with audit reason (`200`)
- `POST /api/v1/platform/tenants/{tenantId}/reactivate` — Reactivate suspended tenant (`200`)
- `GET /api/v1/platform/plans` / `POST` / `PUT` — Manage subscription plan catalog (`200`/`201`)
- `GET /api/v1/platform/audit-logs` — Query platform & cross-tenant security events (`200`)
