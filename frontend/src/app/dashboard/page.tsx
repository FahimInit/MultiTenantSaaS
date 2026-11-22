"use client";

import * as React from "react";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import {
  Activity,
  AlertTriangle,
  ArrowUpRight,
  CreditCard,
  Mail,
  Shield,
  Users,
  Zap,
} from "lucide-react";
import { dashboardApi } from "@/lib/saas-api";
import {
  extractErrorMessage,
  formatDateShort,
  formatDateTime,
  formatNumber,
} from "@/lib/formatters";
import { useAuthTenant } from "@/providers/AuthTenantProvider";
import { AuthGuard } from "@/components/shared/WorkspaceGuard";
import { AppShell } from "@/components/layout/AppShell";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Badge, statusBadgeVariant } from "@/components/ui/badge";
import { Alert } from "@/components/ui/alert";
import {
  ProgressBar,
  Skeleton,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/data-display";

function UsageMeter({
  label,
  used,
  max,
  percent,
}: {
  label: string;
  used: number;
  max: number | null;
  percent: number | null;
}) {
  return (
    <div className="space-y-1.5">
      <div className="flex items-center justify-between text-sm">
        <span className="font-medium">{label}</span>
        <span className="font-mono text-xs text-muted-foreground">
          {formatNumber(used)} / {max === null ? "Unlimited" : formatNumber(max)}
          {percent !== null ? ` (${percent}%)` : ""}
        </span>
      </div>
      <ProgressBar percent={percent ?? 0} />
    </div>
  );
}

export default function DashboardPage() {
  const { activeTenantId, hasPermission } = useAuthTenant();

  const { data, isLoading, error } = useQuery({
    queryKey: ["tenant", activeTenantId, "dashboard"],
    queryFn: () => dashboardApi.getTenantDashboard(),
    enabled: Boolean(activeTenantId) && hasPermission("dashboard.read"),
  });

  return (
    <AuthGuard requireTenant requiredPermission="dashboard.read">
      <AppShell>
        <div className="space-y-6">
          <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-center">
            <div>
              <h1 className="text-2xl font-semibold tracking-tight">
                Organization Overview
              </h1>
              <p className="mt-1 text-sm text-muted-foreground">
                Real-time workspace capacity, subscription status, and audit
                activity.
              </p>
            </div>
            {data && (
              <div className="flex items-center gap-2">
                <Badge variant={statusBadgeVariant(data.tenantStatus)}>
                  Tenant: {data.tenantStatus}
                </Badge>
                <Badge variant={statusBadgeVariant(data.subscription.status)}>
                  Plan: {data.subscription.planName} ({data.subscription.status})
                </Badge>
              </div>
            )}
          </div>

          {error && (
            <Alert variant="destructive" title="Failed to Load Dashboard">
              {extractErrorMessage(error)}
            </Alert>
          )}

          {data && !data.onboardingCompleted && (
            <Alert variant="warning" title="Onboarding Incomplete">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <span>
                  Finish organization setup to mark onboarding as completed.
                </span>
                <Link
                  href="/onboarding"
                  className="inline-flex items-center gap-1 font-medium underline"
                >
                  Open Onboarding <ArrowUpRight className="h-3.5 w-3.5" />
                </Link>
              </div>
            </Alert>
          )}

          {data?.subscription.isTrialExpired && (
            <Alert variant="destructive" title="Trial Expired">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <span>
                  Your trial ended. Upgrade your plan to restore workspace write
                  operations.
                </span>
                <Link
                  href="/subscription"
                  className="inline-flex items-center gap-1 font-medium underline"
                >
                  Upgrade Subscription <ArrowUpRight className="h-3.5 w-3.5" />
                </Link>
              </div>
            </Alert>
          )}

          {isLoading ? (
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              {Array.from({ length: 4 }).map((_, i) => (
                <Card key={i}>
                  <CardContent className="pt-5">
                    <Skeleton className="mb-2 h-4 w-24" />
                    <Skeleton className="h-8 w-16" />
                  </CardContent>
                </Card>
              ))}
            </div>
          ) : (
            data && (
              <>
                <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
                  <Card>
                    <CardHeader className="flex flex-row items-center justify-between pb-2">
                      <CardTitle className="text-sm font-medium text-muted-foreground">
                        Active Members
                      </CardTitle>
                      <Users className="h-4 w-4 text-muted-foreground" />
                    </CardHeader>
                    <CardContent>
                      <p className="text-2xl font-bold">
                        {formatNumber(data.memberStats.activeMembers)}
                      </p>
                      <p className="mt-1 text-xs text-muted-foreground">
                        {data.memberStats.suspendedMembers} suspended ·{" "}
                        {data.memberStats.totalMembers} total memberships
                      </p>
                    </CardContent>
                  </Card>

                  <Card>
                    <CardHeader className="flex flex-row items-center justify-between pb-2">
                      <CardTitle className="text-sm font-medium text-muted-foreground">
                        Pending Invitations
                      </CardTitle>
                      <Mail className="h-4 w-4 text-muted-foreground" />
                    </CardHeader>
                    <CardContent>
                      <p className="text-2xl font-bold">
                        {formatNumber(data.memberStats.pendingInvitations)}
                      </p>
                      <p className="mt-1 text-xs text-muted-foreground">
                        {data.memberStats.rolesCount} roles (
                        {data.memberStats.customRolesCount} custom)
                      </p>
                    </CardContent>
                  </Card>

                  <Card>
                    <CardHeader className="flex flex-row items-center justify-between pb-2">
                      <CardTitle className="text-sm font-medium text-muted-foreground">
                        Subscription
                      </CardTitle>
                      <CreditCard className="h-4 w-4 text-muted-foreground" />
                    </CardHeader>
                    <CardContent>
                      <p className="text-2xl font-bold">
                        {data.subscription.planName}
                      </p>
                      <p className="mt-1 text-xs text-muted-foreground">
                        {data.subscription.billingCycle} ·{" "}
                        {data.subscription.daysRemaining}d remaining (ends{" "}
                        {formatDateShort(data.subscription.currentPeriodEndUtc)})
                      </p>
                    </CardContent>
                  </Card>

                  <Card>
                    <CardHeader className="flex flex-row items-center justify-between pb-2">
                      <CardTitle className="text-sm font-medium text-muted-foreground">
                        Monthly API Calls
                      </CardTitle>
                      <Zap className="h-4 w-4 text-muted-foreground" />
                    </CardHeader>
                    <CardContent>
                      <p className="text-2xl font-bold">
                        {formatNumber(data.usage.monthlyApiCallsUsed)}
                      </p>
                      <p className="mt-1 text-xs text-muted-foreground">
                        {data.usage.maxMonthlyApiCalls === null
                          ? "Unlimited monthly quota"
                          : `Quota: ${formatNumber(data.usage.maxMonthlyApiCalls)}`}
                      </p>
                    </CardContent>
                  </Card>
                </div>

                <div className="grid gap-6 lg:grid-cols-3">
                  <Card className="min-w-0 lg:col-span-1">
                    <CardHeader>
                      <CardTitle className="flex items-center gap-2">
                        <Shield className="h-4 w-4" />
                        Plan Utilization
                      </CardTitle>
                      <CardDescription>
                        Live quota consumption enforced by subscription limits.
                      </CardDescription>
                    </CardHeader>
                    <CardContent className="space-y-5">
                      <UsageMeter
                        label="Organization Seats"
                        used={data.usage.activeMembersUsed}
                        max={data.usage.maxMembers}
                        percent={data.usage.membersUtilizationPercent}
                      />
                      <UsageMeter
                        label="Custom Roles"
                        used={data.usage.customRolesUsed}
                        max={data.usage.maxCustomRoles}
                        percent={data.usage.customRolesUtilizationPercent}
                      />
                      <UsageMeter
                        label="Monthly API Requests"
                        used={data.usage.monthlyApiCallsUsed}
                        max={data.usage.maxMonthlyApiCalls}
                        percent={data.usage.apiCallsUtilizationPercent}
                      />
                      {(data.usage.membersUtilizationPercent ?? 0) >= 85 && (
                        <Alert variant="warning">
                          <span className="flex items-center gap-1.5 text-xs">
                            <AlertTriangle className="h-3.5 w-3.5 shrink-0" />
                            Seat quota nearly reached.
                          </span>
                        </Alert>
                      )}
                    </CardContent>
                  </Card>

                  <Card className="min-w-0 lg:col-span-2">
                    <CardHeader className="flex flex-row items-center justify-between">
                      <div>
                        <CardTitle className="flex items-center gap-2">
                          <Activity className="h-4 w-4" />
                          Recent Workspace Activity
                        </CardTitle>
                        <CardDescription>
                          Latest security, billing, and membership audit events.
                        </CardDescription>
                      </div>
                      {hasPermission("audit.read") && (
                        <Link
                          href="/audit-logs"
                          className="text-xs font-medium text-primary hover:underline"
                        >
                          View all logs
                        </Link>
                      )}
                    </CardHeader>
                    <CardContent>
                      {data.recentActivities.length === 0 ? (
                        <p className="py-8 text-center text-sm text-muted-foreground">
                          No audit events recorded yet for this workspace.
                        </p>
                      ) : (
                        <Table>
                          <TableHeader>
                            <TableRow>
                              <TableHead>Event</TableHead>
                              <TableHead>Category</TableHead>
                              <TableHead>Actor</TableHead>
                              <TableHead className="text-right">Time</TableHead>
                            </TableRow>
                          </TableHeader>
                          <TableBody>
                            {data.recentActivities.map((event) => (
                              <TableRow key={event.id}>
                                <TableCell>
                                  <p className="font-mono text-xs font-medium">
                                    {event.action}
                                  </p>
                                  <p className="line-clamp-1 text-xs text-muted-foreground">
                                    {event.description}
                                  </p>
                                </TableCell>
                                <TableCell>
                                  <Badge variant="outline">
                                    {event.category}
                                  </Badge>
                                </TableCell>
                                <TableCell className="text-xs text-muted-foreground">
                                  {event.actorEmail}
                                </TableCell>
                                <TableCell className="whitespace-nowrap text-right text-xs text-muted-foreground">
                                  {formatDateTime(event.occurredAtUtc)}
                                </TableCell>
                              </TableRow>
                            ))}
                          </TableBody>
                        </Table>
                      )}
                    </CardContent>
                  </Card>
                </div>
              </>
            )
          )}
        </div>
      </AppShell>
    </AuthGuard>
  );
}
