import { apiRequest } from "@/lib/api-client";
import type {
  AcceptInvitationResponse,
  AuditCategory,
  AuditLogDto,
  AuditScope,
  AuthTokenResponse,
  BillingCycle,
  CurrentSubscriptionDto,
  CurrentUserResponse,
  InvitationListItemDto,
  InvitationStatus,
  MemberListItemDto,
  MembershipStatus,
  PagedResult,
  PermissionCatalogItemDto,
  PlatformDashboardDto,
  PlatformTenantDetailDto,
  PlatformTenantListItemDto,
  RegisterUserResponse,
  SubscriptionHistoryDto,
  SubscriptionPlanDto,
  TenantDashboardDto,
  TenantDetailDto,
  TenantRoleSummaryDto,
  TenantSettingsDto,
  TenantStatus,
  UserSessionDto,
  UserTenantSummaryDto,
} from "@/types/api";

export const authApi = {
  register: (payload: { email: string; password: string; fullName: string }) =>
    apiRequest<RegisterUserResponse>("/auth/register", {
      method: "POST",
      body: payload,
      skipAuth: true,
    }),

  verifyEmail: (payload: { userId: string; token: string }) =>
    apiRequest<{ verified: boolean }>("/auth/verify-email", {
      method: "POST",
      body: payload,
      skipAuth: true,
    }),

  resendVerification: (email: string) =>
    apiRequest<{ message: string }>("/auth/resend-verification", {
      method: "POST",
      body: { email },
      skipAuth: true,
    }),

  login: (payload: { email: string; password: string }) =>
    apiRequest<AuthTokenResponse>("/auth/login", {
      method: "POST",
      body: payload,
      skipAuth: true,
    }),

  logout: (refreshToken: string) =>
    apiRequest<void>("/auth/logout", {
      method: "POST",
      body: { refreshToken },
      skipAuth: true,
    }),

  forgotPassword: (email: string) =>
    apiRequest<{ message: string }>("/auth/forgot-password", {
      method: "POST",
      body: { email },
      skipAuth: true,
    }),

  resetPassword: (payload: {
    email: string;
    token: string;
    newPassword: string;
  }) =>
    apiRequest<{ reset: boolean }>("/auth/reset-password", {
      method: "POST",
      body: payload,
      skipAuth: true,
    }),

  me: () => apiRequest<CurrentUserResponse>("/auth/me"),

  listSessions: () => apiRequest<UserSessionDto[]>("/auth/sessions"),

  revokeSession: (sessionId: string) =>
    apiRequest<void>(`/auth/sessions/${sessionId}`, { method: "DELETE" }),
};

export const tenantsApi = {
  listMine: () => apiRequest<UserTenantSummaryDto[]>("/tenants"),

  create: (payload: { name: string; slug: string }) =>
    apiRequest<TenantDetailDto>("/tenants", {
      method: "POST",
      body: payload,
    }),

  getCurrent: (tenantIdOverride?: string) =>
    apiRequest<TenantDetailDto>("/tenants/current", {
      tenantScoped: true,
      tenantIdOverride,
    }),

  updateCurrent: (payload: { name: string; slug: string }) =>
    apiRequest<TenantDetailDto>("/tenants/current", {
      method: "PUT",
      body: payload,
      tenantScoped: true,
    }),

  completeOnboarding: () =>
    apiRequest<TenantDetailDto>("/tenants/current/complete-onboarding", {
      method: "POST",
      tenantScoped: true,
    }),

  getSettings: () =>
    apiRequest<TenantSettingsDto>("/tenants/current/settings", {
      tenantScoped: true,
    }),

  updateSettings: (payload: {
    billingEmail: string;
    supportEmail: string | null;
    timezone: string;
    locale: string;
    enforceEmailDomain: boolean;
    allowedEmailDomains: string[];
    sessionTimeoutMinutes: number;
  }) =>
    apiRequest<TenantSettingsDto>("/tenants/current/settings", {
      method: "PUT",
      body: payload,
      tenantScoped: true,
    }),
};

export const dashboardApi = {
  getTenantDashboard: () =>
    apiRequest<TenantDashboardDto>("/dashboard", { tenantScoped: true }),
};

export const membersApi = {
  list: (params: {
    page?: number;
    pageSize?: number;
    status?: MembershipStatus | "";
    roleId?: string;
    search?: string;
    sortBy?: string;
    sortDirection?: "asc" | "desc";
  }) =>
    apiRequest<PagedResult<MemberListItemDto>>("/members", {
      tenantScoped: true,
      query: params,
    }),

  updateRole: (memberId: string, roleId: string) =>
    apiRequest<MemberListItemDto>(`/members/${memberId}/role`, {
      method: "PUT",
      body: { roleId },
      tenantScoped: true,
    }),

  updateStatus: (memberId: string, status: MembershipStatus) =>
    apiRequest<MemberListItemDto>(`/members/${memberId}/status`, {
      method: "PUT",
      body: { status },
      tenantScoped: true,
    }),

  remove: (memberId: string) =>
    apiRequest<void>(`/members/${memberId}`, {
      method: "DELETE",
      tenantScoped: true,
    }),
};

export const invitationsApi = {
  list: (params: {
    page?: number;
    pageSize?: number;
    status?: InvitationStatus | "";
    search?: string;
    sortDirection?: "asc" | "desc";
  }) =>
    apiRequest<PagedResult<InvitationListItemDto>>("/invitations", {
      tenantScoped: true,
      query: params,
    }),

  invite: (payload: { email: string; roleId: string }) =>
    apiRequest<InvitationListItemDto>("/invitations", {
      method: "POST",
      body: payload,
      tenantScoped: true,
    }),

  resend: (invitationId: string) =>
    apiRequest<InvitationListItemDto>(`/invitations/${invitationId}/resend`, {
      method: "POST",
      tenantScoped: true,
    }),

  revoke: (invitationId: string) =>
    apiRequest<void>(`/invitations/${invitationId}`, {
      method: "DELETE",
      tenantScoped: true,
    }),

  accept: (token: string) =>
    apiRequest<AcceptInvitationResponse>("/invitations/accept", {
      method: "POST",
      body: { token },
    }),
};

export const rolesApi = {
  listPermissions: () =>
    apiRequest<PermissionCatalogItemDto[]>("/permissions", {
      tenantScoped: true,
    }),

  listRoles: () =>
    apiRequest<TenantRoleSummaryDto[]>("/roles", { tenantScoped: true }),

  createRole: (payload: {
    name: string;
    description: string;
    permissions: string[];
  }) =>
    apiRequest<TenantRoleSummaryDto>("/roles", {
      method: "POST",
      body: payload,
      tenantScoped: true,
    }),

  updateRole: (
    roleId: string,
    payload: { name: string; description: string; permissions: string[] }
  ) =>
    apiRequest<TenantRoleSummaryDto>(`/roles/${roleId}`, {
      method: "PUT",
      body: payload,
      tenantScoped: true,
    }),

  deleteRole: (roleId: string) =>
    apiRequest<void>(`/roles/${roleId}`, {
      method: "DELETE",
      tenantScoped: true,
    }),
};

export const subscriptionsApi = {
  listPublicPlans: () =>
    apiRequest<SubscriptionPlanDto[]>("/subscriptions/plans", {
      skipAuth: true,
    }),

  getCurrent: () =>
    apiRequest<CurrentSubscriptionDto>("/subscriptions/current", {
      tenantScoped: true,
    }),

  changePlan: (payload: {
    planCode: string;
    billingCycle: BillingCycle;
    paymentMethodToken?: string | null;
  }) =>
    apiRequest<CurrentSubscriptionDto>("/subscriptions/change-plan", {
      method: "POST",
      body: payload,
      tenantScoped: true,
    }),

  cancel: (reason?: string) =>
    apiRequest<CurrentSubscriptionDto>("/subscriptions/cancel", {
      method: "POST",
      body: { reason: reason || null },
      tenantScoped: true,
    }),

  getHistory: (params: { page?: number; pageSize?: number }) =>
    apiRequest<PagedResult<SubscriptionHistoryDto>>("/subscriptions/history", {
      tenantScoped: true,
      query: params,
    }),
};

export const auditLogsApi = {
  list: (params: {
    page?: number;
    pageSize?: number;
    category?: AuditCategory | "";
    action?: string;
    actorUserId?: string;
    search?: string;
    fromUtc?: string;
    toUtc?: string;
    sortDirection?: "asc" | "desc";
  }) =>
    apiRequest<PagedResult<AuditLogDto>>("/audit-logs", {
      tenantScoped: true,
      query: params,
    }),
};

export const platformApi = {
  getDashboard: () => apiRequest<PlatformDashboardDto>("/platform/dashboard"),

  listTenants: (params: {
    page?: number;
    pageSize?: number;
    search?: string;
    status?: TenantStatus | "";
    sortDirection?: "asc" | "desc";
  }) =>
    apiRequest<PagedResult<PlatformTenantListItemDto>>("/platform/tenants", {
      query: params,
    }),

  getTenantDetail: (tenantId: string) =>
    apiRequest<PlatformTenantDetailDto>(`/platform/tenants/${tenantId}`),

  suspendTenant: (tenantId: string, reason: string) =>
    apiRequest<PlatformTenantDetailDto>(
      `/platform/tenants/${tenantId}/suspend`,
      { method: "POST", body: { reason } }
    ),

  reactivateTenant: (tenantId: string) =>
    apiRequest<PlatformTenantDetailDto>(
      `/platform/tenants/${tenantId}/reactivate`,
      { method: "POST" }
    ),

  listPlans: () => apiRequest<SubscriptionPlanDto[]>("/platform/plans"),

  createPlan: (payload: {
    code: string;
    name: string;
    description: string;
    monthlyPriceCents: number;
    annualPriceCents: number;
    trialDays: number;
    maxMembers: number | null;
    maxCustomRoles: number | null;
    maxMonthlyApiCalls: number | null;
    auditRetentionDays: number;
    allowCustomRoles: boolean;
    isActive: boolean;
    isPublic: boolean;
    sortOrder: number;
  }) =>
    apiRequest<SubscriptionPlanDto>("/platform/plans", {
      method: "POST",
      body: payload,
    }),

  updatePlan: (
    planId: string,
    payload: {
      name: string;
      description: string;
      monthlyPriceCents: number;
      annualPriceCents: number;
      trialDays: number;
      maxMembers: number | null;
      maxCustomRoles: number | null;
      maxMonthlyApiCalls: number | null;
      auditRetentionDays: number;
      allowCustomRoles: boolean;
      isActive: boolean;
      isPublic: boolean;
      sortOrder: number;
    }
  ) =>
    apiRequest<SubscriptionPlanDto>(`/platform/plans/${planId}`, {
      method: "PUT",
      body: payload,
    }),

  listAuditLogs: (params: {
    page?: number;
    pageSize?: number;
    tenantId?: string;
    scope?: AuditScope | "";
    category?: AuditCategory | "";
    action?: string;
    search?: string;
    fromUtc?: string;
    toUtc?: string;
    sortDirection?: "asc" | "desc";
  }) =>
    apiRequest<PagedResult<AuditLogDto>>("/platform/audit-logs", {
      query: params,
    }),
};
