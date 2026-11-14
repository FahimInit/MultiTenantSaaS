# Database Entity-Relationship (ER) Diagram

All tables use PostgreSQL snake_case naming and UTC timestamps (`timestamptz`).  
Tenant-scoped tables carry a non-nullable `tenant_id` foreign key and composite keys/constraints where cross-tenant references must be prevented at the SQL engine level.

```mermaid
erDiagram
    users {
        uuid id PK
        string email UK
        string normalized_email UK
        string full_name
        string password_hash
        boolean email_confirmed
        string platform_role
        boolean is_active
        string security_stamp
        timestamptz created_at_utc
        timestamptz last_login_at_utc
    }

    user_sessions {
        uuid id PK
        uuid user_id FK
        string ip_address
        string user_agent
        timestamptz created_at_utc
        timestamptz last_seen_at_utc
        timestamptz revoked_at_utc
    }

    refresh_tokens {
        uuid id PK
        uuid user_id FK
        uuid session_id FK
        uuid token_family_id
        string token_hash UK
        uuid replaced_by_token_id
        timestamptz expires_at_utc
        timestamptz created_at_utc
        timestamptz revoked_at_utc
        string revoked_reason
    }

    tenants {
        uuid id PK
        string name
        string slug UK
        string status
        uuid created_by_user_id FK
        timestamptz onboarding_completed_at_utc
        timestamptz created_at_utc
        timestamptz updated_at_utc
    }

    tenant_settings {
        uuid id PK
        uuid tenant_id FK "UNIQUE"
        string billing_email
        string support_email
        string timezone
        string locale
        boolean enforce_email_domain
        jsonb allowed_domains_json
        int session_timeout_minutes
        timestamptz updated_at_utc
    }

    tenant_roles {
        uuid id PK
        uuid tenant_id FK "Composite UK (tenant_id, id)"
        string code "Composite UK (tenant_id, code)"
        string name "Composite UK (tenant_id, name)"
        string description
        boolean is_system_role
        timestamptz created_at_utc
        timestamptz updated_at_utc
    }

    tenant_role_permissions {
        uuid tenant_id PK "Composite FK (tenant_id, role_id)"
        uuid role_id PK
        string permission_code PK
    }

    tenant_memberships {
        uuid id PK
        uuid tenant_id FK "Composite UK (tenant_id, user_id)"
        uuid user_id FK
        uuid role_id FK "Composite FK (tenant_id, role_id)"
        string status
        uuid invited_by_user_id FK
        timestamptz joined_at_utc
        timestamptz updated_at_utc
    }

    tenant_invitations {
        uuid id PK
        uuid tenant_id FK
        string email
        string normalized_email "Partial UK when status=Pending"
        uuid role_id FK "Composite FK (tenant_id, role_id)"
        string token_hash UK
        string status
        uuid invited_by_user_id FK
        timestamptz expires_at_utc
        timestamptz accepted_at_utc
        uuid accepted_by_user_id FK
        timestamptz created_at_utc
    }

    subscription_plans {
        uuid id PK
        string code UK
        string name
        string description
        bigint monthly_price_cents
        bigint annual_price_cents
        int trial_days
        int max_members
        int max_custom_roles
        int max_monthly_api_calls
        int audit_retention_days
        boolean allow_custom_roles
        boolean is_active
        boolean is_public
        int sort_order
    }

    tenant_subscriptions {
        uuid id PK
        uuid tenant_id FK "UNIQUE"
        uuid plan_id FK
        string status
        string billing_cycle
        timestamptz current_period_start_utc
        timestamptz current_period_end_utc
        timestamptz trial_ends_at_utc
        timestamptz canceled_at_utc
        boolean auto_renew
        string external_provider_subscription_id
        timestamptz updated_at_utc
    }

    subscription_history {
        uuid id PK
        uuid tenant_id FK
        uuid subscription_id FK
        uuid from_plan_id FK
        uuid to_plan_id FK
        string event_type
        bigint amount_cents
        string currency
        string payment_reference
        uuid actor_user_id
        string notes
        timestamptz occurred_at_utc
    }

    tenant_usage_snapshots {
        uuid id PK
        uuid tenant_id FK "Composite UK (tenant_id, period_start_utc)"
        timestamptz period_start_utc
        timestamptz period_end_utc
        int active_members_count
        int custom_roles_count
        bigint api_calls_count
        timestamptz updated_at_utc
    }

    audit_logs {
        uuid id PK
        uuid tenant_id FK "Nullable for Platform scope"
        uuid actor_user_id
        string actor_email
        string scope
        string category
        string action
        string target_type
        string target_id
        string description
        jsonb metadata_json
        string ip_address
        string user_agent
        string correlation_id
        timestamptz occurred_at_utc
    }

    users ||--o{ user_sessions : owns
    users ||--o{ refresh_tokens : holds
    user_sessions ||--o{ refresh_tokens : issues
    users ||--o{ tenants : creates
    users ||--o{ tenant_memberships : joins

    tenants ||--|| tenant_settings : configures
    tenants ||--o{ tenant_roles : defines
    tenants ||--o{ tenant_memberships : contains
    tenants ||--o{ tenant_invitations : issues
    tenants ||--|| tenant_subscriptions : subscribes
    tenants ||--o{ subscription_history : logs_billing
    tenants ||--o{ tenant_usage_snapshots : measures
    tenants ||--o{ audit_logs : records

    tenant_roles ||--o{ tenant_role_permissions : grants
    tenant_roles ||--o{ tenant_memberships : assigned_to
    tenant_roles ||--o{ tenant_invitations : assigns

    subscription_plans ||--o{ tenant_subscriptions : governs
    tenant_subscriptions ||--o{ subscription_history : tracks
```
