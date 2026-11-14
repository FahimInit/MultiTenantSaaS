export type TenantStatus = "Trial" | "Active" | "Suspended" | "Deactivated";
export type MembershipStatus = "Active" | "Suspended" | "Removed";
export type InvitationStatus = "Pending" | "Accepted" | "Revoked" | "Expired";
export type BillingCycle = "Monthly" | "Annual";
export type SubscriptionStatus =
  | "Trialing"
  | "Active"
  | "PastDue"
  | "Canceled"
  | "Expired";
export type SubscriptionEventType =
  | "TrialStarted"
  | "Subscribed"
  | "Upgraded"
  | "Downgraded"
  | "Canceled"
  | "Renewed"
  | "Expired";
export type AuditScope = "Tenant" | "Platform";
export type AuditCategory =
  | "Identity"
  | "Tenant"
  | "Membership"
  | "AccessControl"
  | "Subscription"
  | "Security"
  | "Platform";

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errorCode?: string;
  correlationId?: string;
  errors?: Record<string, string[]>;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface UserTenantSummaryDto {
  tenantId: string;
  name: string;
  slug: string;
  status: TenantStatus;
  roleCode: string;
  roleName: string;
  onboardingCompleted: boolean;
}

export interface AuthenticatedUserDto {
  id: string;
  email: string;
  fullName: string;
  platformRole: string | null;
}

export interface AuthTokenResponse {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
  user: AuthenticatedUserDto;
  tenants: UserTenantSummaryDto[];
}

export interface RegisterUserResponse {
  userId: string;
  email: string;
  emailVerificationRequired: boolean;
}

export interface UserSessionDto {
  id: string;
  ipAddress: string;
  userAgent: string;
  createdAtUtc: string;
  lastSeenAtUtc: string;
  isActive: boolean;
}

export interface CurrentUserResponse {
  id: string;
  email: string;
  fullName: string;
  emailConfirmed: boolean;
  platformRole: string | null;
  tenants: UserTenantSummaryDto[];
}

export interface TenantDetailDto {
  id: string;
  name: string;
  slug: string;
  status: TenantStatus;
  onboardingCompleted: boolean;
  onboardingCompletedAtUtc: string | null;
  createdAtUtc: string;
  currentUserRoleCode: string;
  currentUserPermissions: string[];
}

export interface TenantSettingsDto {
  tenantId: string;
  billingEmail: string;
  supportEmail: string | null;
  timezone: string;
  locale: string;
  enforceEmailDomain: boolean;
  allowedEmailDomains: string[];
  sessionTimeoutMinutes: number;
  updatedAtUtc: string;
}

export interface MemberListItemDto {
  membershipId: string;
  userId: string;
  email: string;
  fullName: string;
  roleId: string;
  roleCode: string;
  roleName: string;
  isSystemRole: boolean;
  status: MembershipStatus;
  joinedAtUtc: string;
  lastLoginAtUtc: string | null;
}

export interface InvitationListItemDto {
  id: string;
  email: string;
  roleId: string;
  roleCode: string;
  roleName: string;
  status: InvitationStatus;
  invitedByEmail: string;
  expiresAtUtc: string;
  createdAtUtc: string;
  acceptedAtUtc: string | null;
}

export interface AcceptInvitationResponse {
  tenantId: string;
  tenantName: string;
  tenantSlug: string;
  roleCode: string;
  roleName: string;
}

export interface PermissionCatalogItemDto {
  code: string;
  module: string;
  description: string;
}

export interface TenantRoleSummaryDto {
  id: string;
  code: string;
  name: string;
  description: string;
  isSystemRole: boolean;
  memberCount: number;
  permissions: string[];
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface SubscriptionPlanDto {
  id: string;
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
}

export interface DashboardMemberStatsDto {
  totalMembers: number;
  activeMembers: number;
  suspendedMembers: number;
  pendingInvitations: number;
  rolesCount: number;
  customRolesCount: number;
}

export interface DashboardSubscriptionDto {
  planCode: string;
  planName: string;
  status: SubscriptionStatus;
  billingCycle: BillingCycle;
  currentPeriodEndUtc: string;
  trialEndsAtUtc: string | null;
  autoRenew: boolean;
  isTrialExpired: boolean;
  daysRemaining: number;
}

export interface DashboardUsageDto {
  activeMembersUsed: number;
  maxMembers: number | null;
  membersUtilizationPercent: number | null;
  customRolesUsed: number;
  maxCustomRoles: number | null;
  customRolesUtilizationPercent: number | null;
  monthlyApiCallsUsed: number;
  maxMonthlyApiCalls: number | null;
  apiCallsUtilizationPercent: number | null;
}

export interface CurrentSubscriptionDto {
  subscriptionId: string;
  plan: SubscriptionPlanDto;
  status: SubscriptionStatus;
  billingCycle: BillingCycle;
  currentPeriodStartUtc: string;
  currentPeriodEndUtc: string;
  trialEndsAtUtc: string | null;
  canceledAtUtc: string | null;
  autoRenew: boolean;
  isTrialExpired: boolean;
  allowsWorkspaceWrites: boolean;
  daysRemaining: number;
  usage: DashboardUsageDto;
}

export interface SubscriptionHistoryDto {
  id: string;
  fromPlanId: string | null;
  fromPlanName: string | null;
  toPlanId: string;
  toPlanName: string;
  eventType: SubscriptionEventType;
  amountCents: number;
  currency: string;
  paymentReference: string | null;
  actorUserId: string | null;
  notes: string;
  occurredAtUtc: string;
}

export interface AuditLogDto {
  id: string;
  tenantId: string | null;
  tenantName: string | null;
  actorUserId: string | null;
  actorEmail: string;
  scope: AuditScope;
  category: AuditCategory;
  action: string;
  targetType: string;
  targetId: string;
  description: string;
  metadataJson: string | null;
  ipAddress: string | null;
  correlationId: string | null;
  occurredAtUtc: string;
}

export interface TenantDashboardDto {
  tenantId: string;
  tenantName: string;
  tenantSlug: string;
  tenantStatus: TenantStatus;
  onboardingCompleted: boolean;
  memberStats: DashboardMemberStatsDto;
  subscription: DashboardSubscriptionDto;
  usage: DashboardUsageDto;
  recentActivities: AuditLogDto[];
}

export interface PlatformPlanAdoptionDto {
  planCode: string;
  planName: string;
  tenantCount: number;
}

export interface PlatformDashboardDto {
  totalTenants: number;
  activeTenants: number;
  trialTenants: number;
  suspendedTenants: number;
  totalUsers: number;
  verifiedUsers: number;
  estimatedMonthlyRecurringRevenueCents: number;
  planAdoption: PlatformPlanAdoptionDto[];
  recentSecurityAndAdminEvents: AuditLogDto[];
}

export interface PlatformTenantListItemDto {
  id: string;
  name: string;
  slug: string;
  status: TenantStatus;
  onboardingCompleted: boolean;
  activeMembersCount: number;
  planCode: string | null;
  planName: string | null;
  subscriptionStatus: SubscriptionStatus | null;
  createdAtUtc: string;
}

export interface PlatformTenantDetailDto {
  id: string;
  name: string;
  slug: string;
  status: TenantStatus;
  suspensionReason: string | null;
  onboardingCompleted: boolean;
  onboardingCompletedAtUtc: string | null;
  createdAtUtc: string;
  activeMembersCount: number;
  suspendedMembersCount: number;
  pendingInvitationsCount: number;
  customRolesCount: number;
  planCode: string | null;
  planName: string | null;
  subscriptionStatus: SubscriptionStatus | null;
  billingCycle: BillingCycle | null;
  currentPeriodEndUtc: string | null;
  trialEndsAtUtc: string | null;
  currentMonthApiCallsCount: number;
}
