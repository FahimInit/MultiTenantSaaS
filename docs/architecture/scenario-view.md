# 4+1 Scenario View (+1)

The Scenario View validates the Logical, Development, Process, and Physical architectures against seven critical end-to-end business and security scenarios.

---

## Scenario 1: New Organization Registration

```mermaid
sequenceDiagram
    actor Founder
    participant API as "TenantsController"
    participant Handler as "CreateTenantHandler"
    participant DB as "PostgreSQL (Transaction)"

    Founder->>API: "POST /api/v1/tenants { name, slug }"
    API->>Handler: "Execute CreateTenantCommand (CurrentUserId)"
    Handler->>DB: "Begin ACID Transaction"
    Handler->>DB: "Verify email confirmed & slug uniqueness"
    Handler->>DB: "Insert Tenant (Status: Trial)"
    Handler->>DB: "Insert TenantSettings (defaults)"
    Handler->>DB: "Seed 4 System TenantRoles + RolePermissions"
    Handler->>DB: "Insert TenantMembership (Founder -> ORG_OWNER)"
    Handler->>DB: "Insert TenantSubscription (Free or Pro Trial) + SubscriptionHistory"
    Handler->>DB: "Insert AuditLog (TENANT_CREATED)"
    Handler->>DB: "Commit Transaction"
    API-->>Founder: "201 Created { TenantDto }"
```

---

## Scenario 2: User Login

```mermaid
sequenceDiagram
    actor User
    participant API as "AuthController"
    participant Handler as "LoginHandler"
    participant DB as "PostgreSQL"

    User->>API: "POST /api/v1/auth/login { email, password }"
    API->>Handler: "Execute LoginCommand"
    Handler->>DB: "Find user + verify PBKDF2 password + lockout/email check"
    alt "Invalid credentials"
        Handler->>DB: "Increment AccessFailedCount + Write LOGIN_FAILED AuditLog"
        API-->>User: "401 Unauthorized (ProblemDetails)"
    else "Valid credentials"
        Handler->>DB: "Insert UserSession + RefreshToken + Update LastLoginAtUtc"
        Handler->>DB: "Write USER_LOGIN AuditLog + List TenantMemberships"
        API-->>User: "200 OK { accessToken, refreshToken, user, tenants }"
    end
```

1. Client submits `POST /api/v1/auth/login` with `{ email, password }`.
2. `LoginCommandHandler` looks up `ApplicationUser` via `UserManager`, checks `EmailConfirmed`, `IsActive`, and Identity lockout status.
3. On invalid credentials, Identity increments `AccessFailedCount`, writes `LOGIN_FAILED` audit log (without password), and returns `401 Unauthorized` (`ProblemDetails`).
4. On valid credentials, creates `UserSession` + hashed `RefreshToken`, updates `LastLoginAtUtc`, writes `USER_LOGIN` audit log, and returns JWT access token (15m) + opaque refresh token + user's tenant memberships summary.

---

## Scenario 3: Inviting an Employee

```mermaid
sequenceDiagram
    actor Admin as "Org Owner / Admin"
    actor Employee as "Invited Employee"
    participant API as "Members & Invitations API"
    participant DB as "PostgreSQL"
    participant Mail as "IEmailService"

    Admin->>API: "POST /api/v1/members/invitations (X-Tenant-Id, email, roleId)"
    API->>DB: "Verify members.invite, role ownership & Plan.MaxMembers quota"
    API->>DB: "Insert TenantInvitation (SHA-256 TokenHash, 7d expiry) + AuditLog"
    API->>Mail: "Dispatch invitation email"
    API-->>Admin: "201 Created { InvitationDto }"

    Employee->>API: "POST /api/v1/invitations/accept { token }"
    API->>DB: "Verify matching email, Pending status & not expired"
    API->>DB: "Mark Accepted + Insert TenantMembership + Write MEMBER_JOINED AuditLog"
    API-->>Employee: "200 OK { TenantMembershipDto }"
```

1. Organization Admin/Owner sends `POST /api/v1/members/invitations` with `X-Tenant-Id` and `{ email, roleId }`.
2. `TenantResolutionMiddleware` verifies caller has active membership in `X-Tenant-Id` and `PermissionAuthorizationHandler` verifies `members.invite`.
3. `InviteMemberHandler` verifies:
   - Target `roleId` belongs to the **same `TenantId`** and caller cannot assign a role higher than allowed (`ORG_OWNER` only assignable by an Owner).
   - Current `ActiveMembers + PendingInvitations < Plan.MaxMembers`.
   - User is not already an active member and no active pending invite exists for `(TenantId, normalizedEmail)`.
4. Persists `TenantInvitation` with SHA-256 `TokenHash`, 7-day expiry, dispatches email via `IEmailService`, and writes `MEMBER_INVITED` audit log.
5. When employee clicks invite link and calls `POST /api/v1/invitations/accept { token }` while authenticated with matching email, transaction marks invitation `Accepted`, creates `TenantMembership`, and logs `MEMBER_JOINED`.

---

## Scenario 4: Changing Member Permissions / Role

```mermaid
sequenceDiagram
    actor Caller as "Org Owner / Admin"
    participant API as "Members / Roles API"
    participant DB as "PostgreSQL"
    participant Cache as "ICacheService"

    Caller->>API: "PUT /members/{id}/role or PUT /roles/{id}/permissions"
    API->>DB: "Verify caller permissions & role hierarchy"
    API->>DB: "Ensure ORG_OWNER permissions & last active Owner are preserved"
    API->>DB: "Update role/permissions (xmin concurrency) + Write AuditLog"
    API->>Cache: "Bump tenant permission epoch / invalidate membership cache"
    API-->>Caller: "200 OK"
```

1. Organization Owner/Admin invokes `PUT /api/v1/members/{memberId}/role` or `PUT /api/v1/roles/{roleId}/permissions`.
2. **Invariants enforced:**
   - Cannot modify `ORG_OWNER` role permissions (always retains full capabilities).
   - Cannot demote or remove the **last active `ORG_OWNER`** in a tenant.
   - Non-owner cannot promote someone to `ORG_OWNER` or edit a role to contain permissions the caller does not hold.
3. Updates role/membership inside a transaction with optimistic concurrency, invalidates cached membership permissions (Phase 4), and writes `MEMBER_ROLE_CHANGED` or `ROLE_PERMISSIONS_UPDATED` audit record with before/after role metadata.

---

## Scenario 5: Accessing Tenant-Specific Resources

```mermaid
sequenceDiagram
    actor User
    participant MW as "TenantResolutionMiddleware"
    participant AuthZ as "PermissionAuthorizationHandler"
    participant Handler as "Query Handler"
    participant DB as "EF Core + PostgreSQL"

    User->>MW: "GET /api/v1/members?page=1 (Bearer JWT + X-Tenant-Id: OrgA)"
    MW->>DB: "Resolve active TenantMembership for (UserId, OrgA)"
    MW->>AuthZ: "Populate ITenantContext (TenantId=OrgA, Permissions)"
    AuthZ->>Handler: "Authorize permission (members.read)"
    Handler->>DB: "Query WHERE TenantId == OrgA (Global Query Filter + Explicit Filter)"
    DB-->>User: "200 OK (Paged MemberDto)"
```

1. Authenticated user selects Organization A in the UI; frontend attaches `X-Tenant-Id: <OrgA-Id>` to `GET /api/v1/dashboard` or `GET /api/v1/members?page=1&pageSize=20`.
2. `TenantResolutionMiddleware` resolves `(UserId, OrgA-Id)` from `TenantMemberships`, verifies `Tenant.Status != Suspended/Deactivated`, and sets `ITenantContext`.
3. `PermissionAuthorizationHandler` checks `dashboard.read` / `members.read` against resolved `ITenantContext.Permissions`.
4. EF Core `ApplicationDbContext` applies Global Query Filter (`e.TenantId == _tenantContext.TenantId`), AND handler explicitly scopes queries to `TenantId`.
5. Returns DTO projections (`PagedResult<MemberDto>`) — never raw EF entities.

---

## Scenario 6: Subscription Upgrade

```mermaid
sequenceDiagram
    actor Owner as "Organization Owner"
    participant API as "SubscriptionsController"
    participant Pay as "IPaymentProvider"
    participant DB as "PostgreSQL (Transaction)"

    Owner->>API: "POST /api/v1/subscription/change-plan { targetPlanId, billingCycle }"
    API->>DB: "Verify billing.manage & validate member/role counts <= target limits"
    API->>Pay: "ProcessSubscriptionChangeAsync(...)"
    Pay-->>API: "PaymentReference"
    API->>DB: "Update TenantSubscription + Insert SubscriptionHistory + Write AuditLog"
    API-->>Owner: "200 OK { UpdatedSubscriptionDto }"
```

1. Organization Owner calls `POST /api/v1/subscription/change-plan { targetPlanId, billingCycle }` with permission `billing.manage`.
2. Handler validates target plan is active and (if downgrading) current active member count and custom role count fit target plan limits.
3. Calls `IPaymentProvider.ProcessSubscriptionChangeAsync(...)` (`MockPaymentProvider` generates deterministic payment reference).
4. Within ACID transaction: updates `TenantSubscription`, inserts `SubscriptionHistory` (`UPGRADED`), writes `SUBSCRIPTION_UPGRADED` audit record.
5. Next request immediately gains higher member/custom-role quotas.

---

## Scenario 7: Unauthorized Cross-Tenant Access Attempt (Security Test Case)

```mermaid
sequenceDiagram
    actor Attacker as "User A (Member of Tenant A only)"
    participant MW as "TenantResolutionMiddleware"
    participant Handler as "MembersHandler"
    participant Audit as "AuditLog (PostgreSQL)"

    Note over Attacker,Audit: "Attack Vector 1: Spoofed X-Tenant-Id Header for Tenant B"
    Attacker->>MW: "GET /api/v1/members (Header X-Tenant-Id: Tenant-B)"
    MW->>Audit: "Record CROSS_TENANT_ACCESS_DENIED (Actor=UserA, TargetTenant=TenantB)"
    MW-->>Attacker: "403 Forbidden (ProblemDetails)"

    Note over Attacker,Audit: "Attack Vector 2: Valid X-Tenant-Id (Tenant A) + Tenant B Resource ID (IDOR)"
    Attacker->>MW: "PUT /api/v1/members/{TenantB_MemberId}/role (Header X-Tenant-Id: Tenant-A)"
    MW-->>Handler: "ITenantContext = Tenant A (Valid membership)"
    Handler->>Handler: "Query Member WHERE Id = TenantB_MemberId AND TenantId = Tenant A"
    Handler-->>Attacker: "404 Not Found (Zero leakage of Tenant B resource existence)"
```
