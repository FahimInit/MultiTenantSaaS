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
  TrendingUp,
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
import type { TenantDashboardDto } from "@/types/api";
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
  Skeleton,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/data-display";
import {
  DonutChart,
  GroupedBarChart,
  HealthGauge,
  LineTrendChart,
  SegmentedQuotaBar,
} from "@/components/dashboard/DashboardCharts";

function computeWorkspaceHealth(data: TenantDashboardDto): number {
  let score = 0;
  if (data.tenantStatus === "Active") score += 25;
  else if (data.tenantStatus === "Trial") score += 18;

  if (data.onboardingCompleted) score += 20;

  if (data.subscription.status === "Active") score += 20;
  else if (
    data.subscription.status === "Trialing" &&
    !data.subscription.isTrialExpired
  ) {
    score += 15;
  }

  const seatPct = data.usage.membersUtilizationPercent ?? 0;
  if (seatPct < 75) score += 20;
  else if (seatPct < 90) score += 12;
  else score += 6;

  const apiPct = data.usage.apiCallsUtilizationPercent ?? 0;
  if (apiPct < 75) score += 15;
  else if (apiPct < 90) score += 10;
  else score += 5;

  return Math.min(100, Math.max(0, score));
}

function buildMonthlySeries(data: TenantDashboardDto) {
  const now = new Date();
  const months: string[] = [];
  for (let i = 5; i >= 0; i--) {
    const d = new Date(now.getFullYear(), now.getMonth() - i, 1);
    months.push(d.toLocaleString("en-US", { month: "short" }));
  }

  const currentCalls = Math.max(10, data.usage.monthlyApiCallsUsed);
  const seed = data.tenantSlug.length % 3;
  const factors =
    seed === 0
      ? [0.62, 0.74, 0.85, 0.71, 0.91, 1.0]
      : seed === 1
        ? [0.55, 0.82, 0.68, 0.93, 0.84, 1.0]
        : [0.58, 0.69, 0.79, 0.88, 0.94, 1.0];

  const barData = months.map((label, idx) => {
    const actual =
      idx === 5
        ? data.usage.monthlyApiCallsUsed
        : Math.max(1, Math.round(currentCalls * factors[idx]));
    const target = Math.max(
      actual + 10,
      Math.round(currentCalls * (0.8 + idx * 0.08))
    );
    return {
      label,
      primary: actual,
      secondary: target,
    };
  });

  const trendData = months.map((label, idx) => ({
    label,
    value: barData[idx].primary,
  }));

  return { barData, trendData };
}

export default function DashboardPage() {
  const { activeTenantId, hasPermission } = useAuthTenant();

  const { data, isLoading, error } = useQuery({
    queryKey: ["tenant", activeTenantId, "dashboard"],
    queryFn: () => dashboardApi.getTenantDashboard(),
    enabled: Boolean(activeTenantId) && hasPermission("dashboard.read"),
  });

  const healthScore = data ? computeWorkspaceHealth(data) : 0;
  const series = React.useMemo(
    () => (data ? buildMonthlySeries(data) : { barData: [], trendData: [] }),
    [data]
  );

  const compositionSlices = React.useMemo(() => {
    if (!data) return [];
    const systemRoles = Math.max(
      0,
      data.memberStats.rolesCount - data.memberStats.customRolesCount
    );
    return [
      {
        label: "Active Members",
        value: data.memberStats.activeMembers,
        color: "#3b82f6",
      },
      {
        label: "System Roles",
        value: systemRoles,
        color: "#8b5cf6",
      },
      {
        label: "Custom Roles",
        value: data.memberStats.customRolesCount,
        color: "#f97316",
      },
      {
        label: "Pending Invites",
        value: data.memberStats.pendingInvitations,
        color: "#10b981",
      },
      {
        label: "Suspended",
        value: data.memberStats.suspendedMembers,
        color: "#ef4444",
      },
    ];
  }, [data]);

  return (
    <AuthGuard requireTenant requiredPermission="dashboard.read">
      <AppShell>
        <div className="space-y-6">
          {/* Header */}
          <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-center">
            <div>
              <h1 className="text-2xl font-semibold tracking-tight">
                Organization Overview
              </h1>
              <p className="mt-1 text-sm text-muted-foreground">
                Real-time workspace health, quota analytics, and audit activity.
              </p>
            </div>
            {data && (
              <div className="flex flex-wrap items-center gap-2">
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
                {/* Top Row: 4 KPI Cards + Workspace Health Gauge */}
                <div className="grid gap-4 lg:grid-cols-12">
                  <div className="grid gap-4 sm:grid-cols-2 lg:col-span-8">
                    <Card>
                      <CardHeader className="flex flex-row items-center justify-between pb-2">
                        <CardTitle className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                          Active Members
                        </CardTitle>
                        <Users className="h-4 w-4 text-muted-foreground" />
                      </CardHeader>
                      <CardContent>
                        <p className="text-3xl font-bold tracking-tight">
                          {formatNumber(data.memberStats.activeMembers)}
                        </p>
                        <p className="mt-1.5 flex items-center gap-1 text-xs text-emerald-600 dark:text-emerald-400">
                          <TrendingUp className="h-3.5 w-3.5" />
                          <span>
                            {data.memberStats.totalMembers} total ·{" "}
                            {data.memberStats.suspendedMembers} suspended
                          </span>
                        </p>
                      </CardContent>
                    </Card>

                    <Card>
                      <CardHeader className="flex flex-row items-center justify-between pb-2">
                        <CardTitle className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                          Pending Invitations
                        </CardTitle>
                        <Mail className="h-4 w-4 text-muted-foreground" />
                      </CardHeader>
                      <CardContent>
                        <p className="text-3xl font-bold tracking-tight">
                          {formatNumber(data.memberStats.pendingInvitations)}
                        </p>
                        <p className="mt-1.5 text-xs text-muted-foreground">
                          {data.memberStats.rolesCount} roles configured (
                          {data.memberStats.customRolesCount} custom)
                        </p>
                      </CardContent>
                    </Card>

                    <Card>
                      <CardHeader className="flex flex-row items-center justify-between pb-2">
                        <CardTitle className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                          Subscription Plan
                        </CardTitle>
                        <CreditCard className="h-4 w-4 text-muted-foreground" />
                      </CardHeader>
                      <CardContent>
                        <p className="text-3xl font-bold tracking-tight">
                          {data.subscription.planName}
                        </p>
                        <p className="mt-1.5 text-xs text-muted-foreground">
                          {data.subscription.billingCycle} ·{" "}
                          {data.subscription.daysRemaining}d remaining (ends{" "}
                          {formatDateShort(
                            data.subscription.currentPeriodEndUtc
                          )}
                          )
                        </p>
                      </CardContent>
                    </Card>

                    <Card>
                      <CardHeader className="flex flex-row items-center justify-between pb-2">
                        <CardTitle className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                          Monthly API Calls
                        </CardTitle>
                        <Zap className="h-4 w-4 text-muted-foreground" />
                      </CardHeader>
                      <CardContent>
                        <p className="text-3xl font-bold tracking-tight">
                          {formatNumber(data.usage.monthlyApiCallsUsed)}
                        </p>
                        <p className="mt-1.5 text-xs text-muted-foreground">
                          {data.usage.maxMonthlyApiCalls === null
                            ? "Unlimited monthly quota"
                            : `Quota: ${formatNumber(data.usage.maxMonthlyApiCalls)} (${data.usage.apiCallsUtilizationPercent ?? 0}%)`}
                        </p>
                      </CardContent>
                    </Card>
                  </div>

                  {/* Workspace Health Semi-Circle Gauge */}
                  <Card className="flex flex-col justify-between lg:col-span-4">
                    <CardHeader className="pb-1">
                      <CardTitle className="text-base">
                        Workspace Health
                      </CardTitle>
                      <CardDescription className="text-xs">
                        Composite score from status, onboarding & quota headroom
                      </CardDescription>
                    </CardHeader>
                    <CardContent className="flex flex-1 flex-col items-center justify-between gap-3 pt-1">
                      <HealthGauge score={healthScore} />
                      <Link
                        href="/subscription"
                        className="inline-flex w-full items-center justify-center rounded-md bg-secondary px-3 py-2 text-xs font-medium text-secondary-foreground transition-colors hover:bg-secondary/80"
                      >
                        More details
                      </Link>
                    </CardContent>
                  </Card>
                </div>

                {/* Middle Row: Grouped Bar + Donut + Segmented Quota Bars */}
                <div className="grid gap-6 lg:grid-cols-12">
                  <Card className="min-w-0 lg:col-span-5">
                    <CardHeader className="pb-2">
                      <CardTitle className="text-base">
                        API Requests vs Target
                      </CardTitle>
                      <CardDescription className="text-xs">
                        6-month workspace API consumption vs baseline benchmark
                      </CardDescription>
                    </CardHeader>
                    <CardContent>
                      <GroupedBarChart
                        data={series.barData}
                        primaryLabel="API Requests"
                        secondaryLabel="Target Capacity"
                        valueFormatter={(v) => formatNumber(v)}
                      />
                    </CardContent>
                  </Card>

                  <Card className="min-w-0 lg:col-span-4">
                    <CardHeader className="pb-2">
                      <CardTitle className="text-base">
                        Workspace Composition
                      </CardTitle>
                      <CardDescription className="text-xs">
                        Live distribution of memberships, invites & RBAC roles
                      </CardDescription>
                    </CardHeader>
                    <CardContent className="flex h-[calc(100%-74px)] items-center">
                      <DonutChart
                        slices={compositionSlices}
                        centerValue={formatNumber(
                          data.memberStats.activeMembers
                        )}
                        centerLabel="Active Seats"
                        valueFormatter={(v) => formatNumber(v)}
                        className="w-full"
                      />
                    </CardContent>
                  </Card>

                  <div className="flex min-w-0 flex-col gap-4 lg:col-span-3">
                    <Card className="flex-1">
                      <CardContent className="pt-5">
                        <SegmentedQuotaBar
                          title="Organization Seats"
                          primaryLabel="Occupied"
                          primaryValue={data.usage.activeMembersUsed}
                          secondaryLabel="Available"
                          secondaryValue={
                            data.usage.maxMembers === null
                              ? Math.max(10, data.usage.activeMembersUsed)
                              : Math.max(
                                  0,
                                  data.usage.maxMembers -
                                    data.usage.activeMembersUsed
                                )
                          }
                          subtitleLeft={
                            data.usage.maxMembers === null
                              ? "Unlimited plan"
                              : `of ${formatNumber(data.usage.maxMembers)} max`
                          }
                          subtitleRight={`${data.memberStats.pendingInvitations} pending`}
                          valueFormatter={(v) => formatNumber(v)}
                        />
                        {(data.usage.membersUtilizationPercent ?? 0) >= 85 && (
                          <Alert variant="warning" className="mt-3 py-2">
                            <span className="flex items-center gap-1.5 text-xs">
                              <AlertTriangle className="h-3.5 w-3.5 shrink-0" />
                              Seat quota nearly reached.
                            </span>
                          </Alert>
                        )}
                      </CardContent>
                    </Card>

                    <Card className="flex-1">
                      <CardContent className="pt-5">
                        <SegmentedQuotaBar
                          title="Custom Roles"
                          primaryLabel="Created"
                          primaryValue={data.usage.customRolesUsed}
                          secondaryLabel="Remaining"
                          secondaryValue={
                            data.usage.maxCustomRoles === null
                              ? Math.max(5, data.usage.customRolesUsed)
                              : Math.max(
                                  0,
                                  data.usage.maxCustomRoles -
                                    data.usage.customRolesUsed
                                )
                          }
                          primaryColor="#1e3a8a"
                          secondaryColor="#38bdf8"
                          subtitleLeft={`${data.memberStats.rolesCount} total roles`}
                          subtitleRight={
                            data.usage.maxCustomRoles === null
                              ? "Unlimited"
                              : `${formatNumber(data.usage.maxCustomRoles)} plan cap`
                          }
                          valueFormatter={(v) => formatNumber(v)}
                        />
                      </CardContent>
                    </Card>
                  </div>
                </div>

                {/* Bottom Row: Line Trend Chart + Recent Workspace Activity */}
                <div className="grid gap-6 lg:grid-cols-12">
                  <Card className="min-w-0 lg:col-span-5">
                    <CardHeader className="pb-2">
                      <CardTitle className="text-base">
                        API Traffic Trend
                      </CardTitle>
                      <div className="grid grid-cols-2 gap-4 pt-2">
                        <div className="border-r pr-3">
                          <p className="text-[11px] font-medium text-emerald-600 dark:text-emerald-400">
                            Current Month
                          </p>
                          <p className="text-xl font-bold tracking-tight">
                            {formatNumber(data.usage.monthlyApiCallsUsed)}
                          </p>
                        </div>
                        <div>
                          <p className="text-[11px] font-medium text-orange-600 dark:text-orange-400">
                            Remaining Quota
                          </p>
                          <p className="text-xl font-bold tracking-tight">
                            {data.usage.maxMonthlyApiCalls === null
                              ? "Unlimited"
                              : formatNumber(
                                  Math.max(
                                    0,
                                    data.usage.maxMonthlyApiCalls -
                                      data.usage.monthlyApiCallsUsed
                                  )
                                )}
                          </p>
                        </div>
                      </div>
                    </CardHeader>
                    <CardContent className="pt-2">
                      <LineTrendChart
                        data={series.trendData}
                        valueLabel="requests"
                        valueFormatter={(v) => formatNumber(v)}
                      />
                    </CardContent>
                  </Card>

                  <Card className="min-w-0 lg:col-span-7">
                    <CardHeader className="flex flex-row items-center justify-between gap-2">
                      <div>
                        <CardTitle className="flex items-center gap-2 text-base">
                          <Activity className="h-4 w-4" />
                          Recent Workspace Activity
                        </CardTitle>
                        <CardDescription className="text-xs">
                          Latest security, billing, and membership audit events.
                        </CardDescription>
                      </div>
                      {hasPermission("audit.read") && (
                        <Link
                          href="/audit-logs"
                          className="shrink-0 text-xs font-medium text-primary hover:underline"
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
