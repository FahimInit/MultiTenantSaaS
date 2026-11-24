"use client";

import * as React from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import {
  Activity,
  Ban,
  Building2,
  CheckCircle2,
  CreditCard,
  Edit2,
  Eye,
  Plus,
  ShieldCheck,
  Users,
} from "lucide-react";
import { platformApi } from "@/lib/saas-api";
import {
  extractErrorMessage,
  formatCurrencyCents,
  formatDateTime,
  formatNumber,
  prettyPrintJson,
} from "@/lib/formatters";
import type {
  AuditCategory,
  AuditLogDto,
  AuditScope,
  PlatformTenantDetailDto,
  SubscriptionPlanDto,
  TenantStatus,
} from "@/types/api";
import { AuthGuard } from "@/components/shared/WorkspaceGuard";
import { AppShell } from "@/components/layout/AppShell";
import { PaginationControls } from "@/components/shared/PaginationControls";
import { Button } from "@/components/ui/button";
import { Input, Label, Select, Textarea } from "@/components/ui/form-controls";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Badge, statusBadgeVariant } from "@/components/ui/badge";
import { Alert } from "@/components/ui/alert";
import { Dialog } from "@/components/ui/dialog";
import {
  Switch,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/data-display";
import {
  DonutChart,
  HealthGauge,
  SegmentedQuotaBar,
} from "@/components/dashboard/DashboardCharts";

const planFormSchema = z.object({
  code: z.string().trim().min(2).max(32),
  name: z.string().trim().min(2).max(80),
  description: z.string().trim().max(300),
  monthlyPriceCents: z.coerce.number().int().min(0),
  annualPriceCents: z.coerce.number().int().min(0),
  trialDays: z.coerce.number().int().min(0),
  maxMembers: z.string(),
  maxCustomRoles: z.string(),
  maxMonthlyApiCalls: z.string(),
  auditRetentionDays: z.coerce.number().int().min(1),
  allowCustomRoles: z.boolean(),
  isActive: z.boolean(),
  isPublic: z.boolean(),
  sortOrder: z.coerce.number().int().min(0),
});

type PlanFormValues = z.infer<typeof planFormSchema>;

function parseNullableInt(raw: string): number | null {
  const trimmed = raw.trim();
  if (!trimmed) return null;
  const parsed = Number.parseInt(trimmed, 10);
  return Number.isNaN(parsed) ? null : parsed;
}

export default function PlatformPage() {
  const queryClient = useQueryClient();

  const [tab, setTab] = React.useState<
    "overview" | "tenants" | "plans" | "audit"
  >("overview");
  const [errorMsg, setErrorMsg] = React.useState<string | null>(null);
  const [successMsg, setSuccessMsg] = React.useState<string | null>(null);

  const [tenantPage, setTenantPage] = React.useState(1);
  const [tenantSearch, setTenantSearch] = React.useState("");
  const [tenantStatus, setTenantStatus] = React.useState<TenantStatus | "">("");

  const [detailTenant, setDetailTenant] =
    React.useState<PlatformTenantDetailDto | null>(null);
  const [suspendingTenantId, setSuspendingTenantId] = React.useState<
    string | null
  >(null);
  const [suspendReason, setSuspendReason] = React.useState("");

  const [planDialogOpen, setPlanDialogOpen] = React.useState(false);
  const [editingPlan, setEditingPlan] =
    React.useState<SubscriptionPlanDto | null>(null);

  const [auditPage, setAuditPage] = React.useState(1);
  const [auditScope, setAuditScope] = React.useState<AuditScope | "">("");
  const [auditCategory, setAuditCategory] = React.useState<AuditCategory | "">(
    ""
  );
  const [auditSearch, setAuditSearch] = React.useState("");
  const [selectedAudit, setSelectedAudit] = React.useState<AuditLogDto | null>(
    null
  );

  const dashboardQuery = useQuery({
    queryKey: ["platform", "dashboard"],
    queryFn: () => platformApi.getDashboard(),
  });

  const tenantsQuery = useQuery({
    queryKey: [
      "platform",
      "tenants",
      { tenantPage, tenantSearch, tenantStatus },
    ],
    queryFn: () =>
      platformApi.listTenants({
        page: tenantPage,
        pageSize: 15,
        search: tenantSearch || undefined,
        status: tenantStatus,
      }),
    enabled: tab === "tenants",
  });

  const plansQuery = useQuery({
    queryKey: ["platform", "plans"],
    queryFn: () => platformApi.listPlans(),
    enabled: tab === "plans",
  });

  const auditQuery = useQuery({
    queryKey: [
      "platform",
      "auditLogs",
      { auditPage, auditScope, auditCategory, auditSearch },
    ],
    queryFn: () =>
      platformApi.listAuditLogs({
        page: auditPage,
        pageSize: 20,
        scope: auditScope,
        category: auditCategory,
        search: auditSearch || undefined,
      }),
    enabled: tab === "audit",
  });

  const planForm = useForm<PlanFormValues>({
    resolver: zodResolver(planFormSchema),
    defaultValues: {
      code: "",
      name: "",
      description: "",
      monthlyPriceCents: 0,
      annualPriceCents: 0,
      trialDays: 14,
      maxMembers: "",
      maxCustomRoles: "",
      maxMonthlyApiCalls: "",
      auditRetentionDays: 90,
      allowCustomRoles: true,
      isActive: true,
      isPublic: true,
      sortOrder: 10,
    },
  });

  const invalidatePlatform = () => {
    void queryClient.invalidateQueries({ queryKey: ["platform"] });
    void queryClient.invalidateQueries({ queryKey: ["publicPlans"] });
  };

  const inspectTenantMutation = useMutation({
    mutationFn: (id: string) => platformApi.getTenantDetail(id),
    onSuccess: (detail) => setDetailTenant(detail),
    onError: (err) => setErrorMsg(extractErrorMessage(err)),
  });

  const suspendMutation = useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) =>
      platformApi.suspendTenant(id, reason),
    onSuccess: (updated) => {
      setErrorMsg(null);
      setSuccessMsg(`Suspended tenant "${updated.name}".`);
      setSuspendingTenantId(null);
      setSuspendReason("");
      setDetailTenant(updated);
      invalidatePlatform();
    },
    onError: (err) => setErrorMsg(extractErrorMessage(err)),
  });

  const reactivateMutation = useMutation({
    mutationFn: (id: string) => platformApi.reactivateTenant(id),
    onSuccess: (updated) => {
      setErrorMsg(null);
      setSuccessMsg(`Reactivated tenant "${updated.name}".`);
      setDetailTenant(updated);
      invalidatePlatform();
    },
    onError: (err) => setErrorMsg(extractErrorMessage(err)),
  });

  const savePlanMutation = useMutation({
    mutationFn: (values: PlanFormValues) => {
      const shared = {
        name: values.name,
        description: values.description,
        monthlyPriceCents: values.monthlyPriceCents,
        annualPriceCents: values.annualPriceCents,
        trialDays: values.trialDays,
        maxMembers: parseNullableInt(values.maxMembers),
        maxCustomRoles: parseNullableInt(values.maxCustomRoles),
        maxMonthlyApiCalls: parseNullableInt(values.maxMonthlyApiCalls),
        auditRetentionDays: values.auditRetentionDays,
        allowCustomRoles: values.allowCustomRoles,
        isActive: values.isActive,
        isPublic: values.isPublic,
        sortOrder: values.sortOrder,
      };
      return editingPlan
        ? platformApi.updatePlan(editingPlan.id, shared)
        : platformApi.createPlan({ code: values.code.toUpperCase(), ...shared });
    },
    onSuccess: (saved) => {
      setErrorMsg(null);
      setSuccessMsg(
        editingPlan
          ? `Updated plan "${saved.name}".`
          : `Created plan "${saved.name}".`
      );
      setPlanDialogOpen(false);
      setEditingPlan(null);
      invalidatePlatform();
    },
    onError: (err) => setErrorMsg(extractErrorMessage(err)),
  });

  const openCreatePlan = () => {
    setEditingPlan(null);
    planForm.reset({
      code: "",
      name: "",
      description: "",
      monthlyPriceCents: 9900,
      annualPriceCents: 99000,
      trialDays: 14,
      maxMembers: "50",
      maxCustomRoles: "10",
      maxMonthlyApiCalls: "500000",
      auditRetentionDays: 180,
      allowCustomRoles: true,
      isActive: true,
      isPublic: true,
      sortOrder: 40,
    });
    setPlanDialogOpen(true);
  };

  const openEditPlan = (plan: SubscriptionPlanDto) => {
    setEditingPlan(plan);
    planForm.reset({
      code: plan.code,
      name: plan.name,
      description: plan.description,
      monthlyPriceCents: plan.monthlyPriceCents,
      annualPriceCents: plan.annualPriceCents,
      trialDays: plan.trialDays,
      maxMembers: plan.maxMembers !== null ? String(plan.maxMembers) : "",
      maxCustomRoles:
        plan.maxCustomRoles !== null ? String(plan.maxCustomRoles) : "",
      maxMonthlyApiCalls:
        plan.maxMonthlyApiCalls !== null ? String(plan.maxMonthlyApiCalls) : "",
      auditRetentionDays: plan.auditRetentionDays,
      allowCustomRoles: plan.allowCustomRoles,
      isActive: plan.isActive,
      isPublic: plan.isPublic,
      sortOrder: plan.sortOrder,
    });
    setPlanDialogOpen(true);
  };

  return (
    <AuthGuard requirePlatformAdmin>
      <AppShell>
        <div className="space-y-6">
          <div className="flex flex-col justify-between gap-2 sm:flex-row sm:items-center">
            <div>
              <h1 className="flex items-center gap-2 text-2xl font-semibold tracking-tight">
                <ShieldCheck className="h-6 w-6 text-primary" />
                Platform Administration Console
              </h1>
              <p className="mt-1 text-sm text-muted-foreground">
                Global multi-tenant operations, tenant lifecycle enforcement,
                plan catalog management, and cross-tenant audit telemetry.
              </p>
            </div>
            <Badge variant="info" className="w-fit self-start sm:self-center">
              PlatformAdministrator
            </Badge>
          </div>

          {errorMsg && (
            <Alert variant="destructive" title="Platform Operation Error">
              {errorMsg}
            </Alert>
          )}
          {successMsg && (
            <Alert variant="success" title="Updated">
              {successMsg}
            </Alert>
          )}

          {/* Navigation Tabs */}
          <div className="flex flex-wrap gap-2 border-b">
            {(
              [
                ["overview", "Overview"],
                ["tenants", "Tenants Directory"],
                ["plans", "Subscription Plans"],
                ["audit", "Global Audit Logs"],
              ] as const
            ).map(([key, label]) => (
              <button
                key={key}
                type="button"
                onClick={() => setTab(key)}
                className={`border-b-2 px-4 py-2 text-sm font-medium transition-colors ${
                  tab === key
                    ? "border-primary text-foreground"
                    : "border-transparent text-muted-foreground hover:text-foreground"
                }`}
              >
                {label}
              </button>
            ))}
          </div>

          {tab === "overview" && dashboardQuery.data && (
            <div className="space-y-6">
              <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
                <Card>
                  <CardHeader className="flex flex-row items-center justify-between pb-2">
                    <CardTitle className="text-sm font-medium text-muted-foreground">
                      Total Organizations
                    </CardTitle>
                    <Building2 className="h-4 w-4 text-muted-foreground" />
                  </CardHeader>
                  <CardContent>
                    <p className="text-2xl font-bold">
                      {dashboardQuery.data.totalTenants}
                    </p>
                    <p className="mt-1 text-xs text-muted-foreground">
                      {dashboardQuery.data.activeTenants} active ·{" "}
                      {dashboardQuery.data.trialTenants} trial ·{" "}
                      {dashboardQuery.data.suspendedTenants} suspended
                    </p>
                  </CardContent>
                </Card>

                <Card>
                  <CardHeader className="flex flex-row items-center justify-between pb-2">
                    <CardTitle className="text-sm font-medium text-muted-foreground">
                      Registered Users
                    </CardTitle>
                    <Users className="h-4 w-4 text-muted-foreground" />
                  </CardHeader>
                  <CardContent>
                    <p className="text-2xl font-bold">
                      {dashboardQuery.data.totalUsers}
                    </p>
                    <p className="mt-1 text-xs text-muted-foreground">
                      {dashboardQuery.data.verifiedUsers} email-verified
                    </p>
                  </CardContent>
                </Card>

                <Card>
                  <CardHeader className="flex flex-row items-center justify-between pb-2">
                    <CardTitle className="text-sm font-medium text-muted-foreground">
                      Estimated MRR
                    </CardTitle>
                    <CreditCard className="h-4 w-4 text-muted-foreground" />
                  </CardHeader>
                  <CardContent>
                    <p className="text-2xl font-bold">
                      {formatCurrencyCents(
                        dashboardQuery.data.estimatedMonthlyRecurringRevenueCents
                      )}
                    </p>
                    <p className="mt-1 text-xs text-muted-foreground">
                      Active paid subscriptions
                    </p>
                  </CardContent>
                </Card>

                <Card>
                  <CardHeader className="flex flex-row items-center justify-between pb-2">
                    <CardTitle className="text-sm font-medium text-muted-foreground">
                      Plan Distribution
                    </CardTitle>
                    <Activity className="h-4 w-4 text-muted-foreground" />
                  </CardHeader>
                  <CardContent className="space-y-1 text-xs">
                    {dashboardQuery.data.planAdoption.map((p) => (
                      <div
                        key={p.planCode}
                        className="flex items-center justify-between"
                      >
                        <span>{p.planName}</span>
                        <span className="font-mono font-semibold">
                          {p.tenantCount}
                        </span>
                      </div>
                    ))}
                  </CardContent>
                </Card>
              </div>

              {/* Platform Visual Analytics Row */}
              <div className="grid gap-6 lg:grid-cols-12">
                <Card className="min-w-0 lg:col-span-4">
                  <CardHeader className="pb-1">
                    <CardTitle className="text-base">Platform Health</CardTitle>
                    <CardDescription className="text-xs">
                      Active tenant ratio & user email verification rate
                    </CardDescription>
                  </CardHeader>
                  <CardContent className="flex items-center justify-center pt-2">
                    <HealthGauge
                      score={Math.round(
                        ((dashboardQuery.data.totalTenants > 0
                          ? dashboardQuery.data.activeTenants /
                            dashboardQuery.data.totalTenants
                          : 1) *
                          50) +
                          ((dashboardQuery.data.totalUsers > 0
                            ? dashboardQuery.data.verifiedUsers /
                              dashboardQuery.data.totalUsers
                            : 1) *
                            50)
                      )}
                    />
                  </CardContent>
                </Card>

                <Card className="min-w-0 lg:col-span-4">
                  <CardHeader className="pb-2">
                    <CardTitle className="text-base">
                      Plan Adoption Mix
                    </CardTitle>
                    <CardDescription className="text-xs">
                      Organizations distributed across subscription tiers
                    </CardDescription>
                  </CardHeader>
                  <CardContent className="flex items-center pt-1">
                    <DonutChart
                      slices={dashboardQuery.data.planAdoption.map((p, i) => ({
                        label: p.planName,
                        value: p.tenantCount,
                        color: [
                          "#3b82f6",
                          "#f97316",
                          "#8b5cf6",
                          "#10b981",
                          "#ef4444",
                        ][i % 5],
                      }))}
                      centerValue={String(dashboardQuery.data.totalTenants)}
                      centerLabel="Orgs"
                      className="w-full"
                    />
                  </CardContent>
                </Card>

                <div className="flex min-w-0 flex-col gap-4 lg:col-span-4">
                  <Card className="flex-1">
                    <CardContent className="pt-5">
                      <SegmentedQuotaBar
                        title="Tenant Lifecycle"
                        primaryLabel="Active"
                        primaryValue={dashboardQuery.data.activeTenants}
                        secondaryLabel="Trial / Suspended"
                        secondaryValue={
                          dashboardQuery.data.trialTenants +
                          dashboardQuery.data.suspendedTenants
                        }
                        subtitleLeft={`${dashboardQuery.data.totalTenants} total orgs`}
                        subtitleRight={`${dashboardQuery.data.suspendedTenants} suspended`}
                      />
                    </CardContent>
                  </Card>

                  <Card className="flex-1">
                    <CardContent className="pt-5">
                      <SegmentedQuotaBar
                        title="User Verification"
                        primaryLabel="Verified"
                        primaryValue={dashboardQuery.data.verifiedUsers}
                        secondaryLabel="Unverified"
                        secondaryValue={Math.max(
                          0,
                          dashboardQuery.data.totalUsers -
                            dashboardQuery.data.verifiedUsers
                        )}
                        primaryColor="#10b981"
                        secondaryColor="#f97316"
                        subtitleLeft={`${dashboardQuery.data.totalUsers} registered`}
                        subtitleRight="Email confirmed"
                      />
                    </CardContent>
                  </Card>
                </div>
              </div>

              <Card className="min-w-0">
                <CardHeader>
                  <CardTitle>Recent Global Security & Admin Events</CardTitle>
                </CardHeader>
                <CardContent>
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Timestamp</TableHead>
                        <TableHead>Scope</TableHead>
                        <TableHead>Action</TableHead>
                        <TableHead>Tenant</TableHead>
                        <TableHead>Actor</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {dashboardQuery.data.recentSecurityAndAdminEvents.map(
                        (ev) => (
                          <TableRow key={ev.id}>
                            <TableCell className="whitespace-nowrap text-xs text-muted-foreground">
                              {formatDateTime(ev.occurredAtUtc)}
                            </TableCell>
                            <TableCell>
                              <Badge variant="outline">{ev.scope}</Badge>
                            </TableCell>
                            <TableCell>
                              <p className="font-mono text-xs font-medium">
                                {ev.action}
                              </p>
                              <p className="text-xs text-muted-foreground">
                                {ev.description}
                              </p>
                            </TableCell>
                            <TableCell className="text-xs">
                              {ev.tenantName ?? "Platform"}
                            </TableCell>
                            <TableCell className="text-xs text-muted-foreground">
                              {ev.actorEmail}
                            </TableCell>
                          </TableRow>
                        )
                      )}
                    </TableBody>
                  </Table>
                </CardContent>
              </Card>
            </div>
          )}

          {tab === "tenants" && (
            <Card>
              <CardHeader>
                <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                  <div>
                    <CardTitle>All Organizations</CardTitle>
                    <CardDescription>
                      Inspect tenant telemetry, suspend abusive workspaces, or
                      reactivate suspended organizations.
                    </CardDescription>
                  </div>
                  <div className="flex gap-2">
                    <Input
                      placeholder="Search org name or slug..."
                      value={tenantSearch}
                      onChange={(e) => {
                        setTenantSearch(e.target.value);
                        setTenantPage(1);
                      }}
                      className="w-52"
                    />
                    <Select
                      aria-label="Filter tenants by status"
                      value={tenantStatus}
                      onChange={(e) => {
                        setTenantStatus(e.target.value as TenantStatus | "");
                        setTenantPage(1);
                      }}
                      className="w-36"
                    >
                      <option value="">All Statuses</option>
                      <option value="Active">Active</option>
                      <option value="Trial">Trial</option>
                      <option value="Suspended">Suspended</option>
                      <option value="Deactivated">Deactivated</option>
                    </Select>
                  </div>
                </div>
              </CardHeader>
              <CardContent>
                {tenantsQuery.isLoading ? (
                  <p className="py-8 text-center text-sm text-muted-foreground">
                    Loading tenants...
                  </p>
                ) : (
                  <>
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Organization</TableHead>
                          <TableHead>Status</TableHead>
                          <TableHead>Plan</TableHead>
                          <TableHead>Seats</TableHead>
                          <TableHead>Created</TableHead>
                          <TableHead className="text-right">Actions</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {tenantsQuery.data?.items.map((t) => (
                          <TableRow key={t.id}>
                            <TableCell>
                              <p className="font-medium">{t.name}</p>
                              <p className="font-mono text-xs text-muted-foreground">
                                /{t.slug}
                              </p>
                            </TableCell>
                            <TableCell>
                              <Badge variant={statusBadgeVariant(t.status)}>
                                {t.status}
                              </Badge>
                            </TableCell>
                            <TableCell className="text-xs">
                              {t.planName ?? "—"}{" "}
                              {t.subscriptionStatus && (
                                <span className="text-muted-foreground">
                                  ({t.subscriptionStatus})
                                </span>
                              )}
                            </TableCell>
                            <TableCell className="font-mono text-xs">
                              {t.activeMembersCount}
                            </TableCell>
                            <TableCell className="text-xs text-muted-foreground">
                              {formatDateTime(t.createdAtUtc)}
                            </TableCell>
                            <TableCell className="text-right">
                              <div className="flex justify-end gap-1.5">
                                <Button
                                  variant="outline"
                                  size="sm"
                                  onClick={() =>
                                    inspectTenantMutation.mutate(t.id)
                                  }
                                >
                                  Inspect
                                </Button>
                                {t.status !== "Suspended" ? (
                                  <Button
                                    variant="destructive"
                                    size="sm"
                                    onClick={() => setSuspendingTenantId(t.id)}
                                  >
                                    <Ban className="h-3.5 w-3.5" />
                                  </Button>
                                ) : (
                                  <Button
                                    variant="outline"
                                    size="sm"
                                    onClick={() =>
                                      reactivateMutation.mutate(t.id)
                                    }
                                  >
                                    <CheckCircle2 className="h-3.5 w-3.5" />
                                  </Button>
                                )}
                              </div>
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                    {tenantsQuery.data && (
                      <PaginationControls
                        page={tenantsQuery.data.page}
                        totalPages={tenantsQuery.data.totalPages}
                        totalCount={tenantsQuery.data.totalCount}
                        pageSize={tenantsQuery.data.pageSize}
                        onPageChange={setTenantPage}
                      />
                    )}
                  </>
                )}
              </CardContent>
            </Card>
          )}

          {tab === "plans" && (
            <Card>
              <CardHeader className="flex flex-row items-center justify-between">
                <div>
                  <CardTitle>Subscription Plan Definitions</CardTitle>
                  <CardDescription>
                    Manage plan pricing, trial windows, quota ceilings, and
                    public visibility.
                  </CardDescription>
                </div>
                <Button onClick={openCreatePlan}>
                  <Plus className="h-4 w-4" />
                  New Plan
                </Button>
              </CardHeader>
              <CardContent>
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Code / Name</TableHead>
                      <TableHead>Monthly</TableHead>
                      <TableHead>Annual</TableHead>
                      <TableHead>Quotas (Seats / Roles / API)</TableHead>
                      <TableHead>Flags</TableHead>
                      <TableHead className="text-right">Edit</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {plansQuery.data?.map((plan) => (
                      <TableRow key={plan.id}>
                        <TableCell>
                          <p className="font-medium">{plan.name}</p>
                          <p className="font-mono text-xs text-muted-foreground">
                            {plan.code}
                          </p>
                        </TableCell>
                        <TableCell className="font-mono text-xs">
                          {formatCurrencyCents(plan.monthlyPriceCents)}
                        </TableCell>
                        <TableCell className="font-mono text-xs">
                          {formatCurrencyCents(plan.annualPriceCents)}
                        </TableCell>
                        <TableCell className="font-mono text-xs text-muted-foreground">
                          {plan.maxMembers ?? "∞"} /{" "}
                          {plan.allowCustomRoles
                            ? (plan.maxCustomRoles ?? "∞")
                            : "0"}{" "}
                          /{" "}
                          {plan.maxMonthlyApiCalls === null
                            ? "∞"
                            : formatNumber(plan.maxMonthlyApiCalls)}
                        </TableCell>
                        <TableCell>
                          <div className="flex flex-wrap gap-1">
                            <Badge
                              variant={plan.isActive ? "success" : "secondary"}
                            >
                              {plan.isActive ? "Active" : "Inactive"}
                            </Badge>
                            {plan.isPublic && (
                              <Badge variant="outline">Public</Badge>
                            )}
                          </div>
                        </TableCell>
                        <TableCell className="text-right">
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => openEditPlan(plan)}
                          >
                            <Edit2 className="h-3.5 w-3.5" />
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}

          {tab === "audit" && (
            <Card>
              <CardHeader>
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div>
                    <CardTitle>Cross-Tenant Global Audit Trail</CardTitle>
                    <CardDescription>
                      Includes platform-level and all tenant-level events.
                    </CardDescription>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Input
                      placeholder="Search logs..."
                      value={auditSearch}
                      onChange={(e) => {
                        setAuditSearch(e.target.value);
                        setAuditPage(1);
                      }}
                      className="w-48"
                    />
                    <Select
                      aria-label="Scope"
                      value={auditScope}
                      onChange={(e) => {
                        setAuditScope(e.target.value as AuditScope | "");
                        setAuditPage(1);
                      }}
                      className="w-32"
                    >
                      <option value="">All Scopes</option>
                      <option value="Tenant">Tenant</option>
                      <option value="Platform">Platform</option>
                    </Select>
                    <Select
                      aria-label="Category"
                      value={auditCategory}
                      onChange={(e) => {
                        setAuditCategory(e.target.value as AuditCategory | "");
                        setAuditPage(1);
                      }}
                      className="w-36"
                    >
                      <option value="">All Categories</option>
                      <option value="Identity">Identity</option>
                      <option value="Tenant">Tenant</option>
                      <option value="Membership">Membership</option>
                      <option value="AccessControl">AccessControl</option>
                      <option value="Subscription">Subscription</option>
                      <option value="Security">Security</option>
                      <option value="Platform">Platform</option>
                    </Select>
                  </div>
                </div>
              </CardHeader>
              <CardContent>
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Timestamp</TableHead>
                      <TableHead>Scope</TableHead>
                      <TableHead>Tenant</TableHead>
                      <TableHead>Action</TableHead>
                      <TableHead>Actor</TableHead>
                      <TableHead className="text-right">Details</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {auditQuery.data?.items.map((log) => (
                      <TableRow key={log.id}>
                        <TableCell className="whitespace-nowrap text-xs text-muted-foreground">
                          {formatDateTime(log.occurredAtUtc)}
                        </TableCell>
                        <TableCell>
                          <Badge variant="outline">{log.scope}</Badge>
                        </TableCell>
                        <TableCell className="text-xs">
                          {log.tenantName ?? "Platform"}
                        </TableCell>
                        <TableCell>
                          <p className="font-mono text-xs font-medium">
                            {log.action}
                          </p>
                          <p className="text-xs text-muted-foreground">
                            {log.description}
                          </p>
                        </TableCell>
                        <TableCell className="text-xs">
                          {log.actorEmail}
                        </TableCell>
                        <TableCell className="text-right">
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => setSelectedAudit(log)}
                          >
                            <Eye className="h-4 w-4" />
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
                {auditQuery.data && (
                  <PaginationControls
                    page={auditQuery.data.page}
                    totalPages={auditQuery.data.totalPages}
                    totalCount={auditQuery.data.totalCount}
                    pageSize={auditQuery.data.pageSize}
                    onPageChange={setAuditPage}
                  />
                )}
              </CardContent>
            </Card>
          )}
        </div>

        {/* Tenant Detail Modal */}
        <Dialog
          open={Boolean(detailTenant)}
          onClose={() => setDetailTenant(null)}
          title={detailTenant?.name ?? "Organization Details"}
          description={`Slug: /${detailTenant?.slug ?? ""}`}
          maxWidthClass="max-w-xl"
        >
          {detailTenant && (
            <div className="space-y-4 text-sm">
              <div className="grid grid-cols-2 gap-3 rounded-md border bg-muted/40 p-3 text-xs">
                <div>
                  <span className="text-muted-foreground">Status:</span>{" "}
                  <Badge variant={statusBadgeVariant(detailTenant.status)}>
                    {detailTenant.status}
                  </Badge>
                </div>
                <div>
                  <span className="text-muted-foreground">Plan:</span>{" "}
                  <strong>{detailTenant.planName ?? "None"}</strong> (
                  {detailTenant.subscriptionStatus ?? "—"})
                </div>
                <div>
                  <span className="text-muted-foreground">Active Seats:</span>{" "}
                  <strong>{detailTenant.activeMembersCount}</strong>
                </div>
                <div>
                  <span className="text-muted-foreground">Pending Invites:</span>{" "}
                  <strong>{detailTenant.pendingInvitationsCount}</strong>
                </div>
                <div>
                  <span className="text-muted-foreground">Custom Roles:</span>{" "}
                  <strong>{detailTenant.customRolesCount}</strong>
                </div>
                <div>
                  <span className="text-muted-foreground">
                    Monthly API Calls:
                  </span>{" "}
                  <strong>
                    {formatNumber(detailTenant.currentMonthApiCallsCount)}
                  </strong>
                </div>
                {detailTenant.suspensionReason && (
                  <div className="col-span-2 text-destructive">
                    Suspension reason: {detailTenant.suspensionReason}
                  </div>
                )}
              </div>
              <div className="flex justify-end gap-2">
                {detailTenant.status !== "Suspended" ? (
                  <Button
                    variant="destructive"
                    onClick={() => setSuspendingTenantId(detailTenant.id)}
                  >
                    Suspend Organization
                  </Button>
                ) : (
                  <Button
                    loading={reactivateMutation.isPending}
                    onClick={() => reactivateMutation.mutate(detailTenant.id)}
                  >
                    Reactivate Organization
                  </Button>
                )}
              </div>
            </div>
          )}
        </Dialog>

        {/* Suspend Reason Modal */}
        <Dialog
          open={Boolean(suspendingTenantId)}
          onClose={() => setSuspendingTenantId(null)}
          title="Suspend Organization"
          description="Suspended tenants are immediately blocked from tenant-scoped API calls across all users."
        >
          <div className="space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="suspend-reason">Reason</Label>
              <Textarea
                id="suspend-reason"
                value={suspendReason}
                onChange={(e) => setSuspendReason(e.target.value)}
                placeholder="Billing delinquency or terms-of-service violation..."
              />
            </div>
            <div className="flex justify-end gap-2">
              <Button
                variant="outline"
                onClick={() => setSuspendingTenantId(null)}
              >
                Cancel
              </Button>
              <Button
                variant="destructive"
                disabled={!suspendReason.trim()}
                loading={suspendMutation.isPending}
                onClick={() => {
                  if (suspendingTenantId && suspendReason.trim()) {
                    suspendMutation.mutate({
                      id: suspendingTenantId,
                      reason: suspendReason.trim(),
                    });
                  }
                }}
              >
                Confirm Suspension
              </Button>
            </div>
          </div>
        </Dialog>

        {/* Create / Edit Plan Modal */}
        <Dialog
          open={planDialogOpen}
          onClose={() => setPlanDialogOpen(false)}
          title={
            editingPlan
              ? `Edit Plan (${editingPlan.code})`
              : "Create Subscription Plan"
          }
          maxWidthClass="max-w-2xl"
        >
          <form
            onSubmit={planForm.handleSubmit((v) => savePlanMutation.mutate(v))}
            className="space-y-4"
          >
            <div className="grid gap-3 sm:grid-cols-2">
              <div className="space-y-1">
                <Label htmlFor="plan-code">Code</Label>
                <Input
                  id="plan-code"
                  disabled={Boolean(editingPlan)}
                  className="font-mono uppercase"
                  {...planForm.register("code")}
                />
              </div>
              <div className="space-y-1">
                <Label htmlFor="plan-name">Display Name</Label>
                <Input id="plan-name" {...planForm.register("name")} />
              </div>
              <div className="space-y-1 sm:col-span-2">
                <Label htmlFor="plan-desc">Description</Label>
                <Input id="plan-desc" {...planForm.register("description")} />
              </div>
              <div className="space-y-1">
                <Label htmlFor="plan-monthly">Monthly Price (cents)</Label>
                <Input
                  id="plan-monthly"
                  type="number"
                  {...planForm.register("monthlyPriceCents")}
                />
              </div>
              <div className="space-y-1">
                <Label htmlFor="plan-annual">Annual Price (cents)</Label>
                <Input
                  id="plan-annual"
                  type="number"
                  {...planForm.register("annualPriceCents")}
                />
              </div>
              <div className="space-y-1">
                <Label htmlFor="plan-members">
                  Max Members (blank = unlimited)
                </Label>
                <Input
                  id="plan-members"
                  type="number"
                  placeholder="Unlimited"
                  {...planForm.register("maxMembers")}
                />
              </div>
              <div className="space-y-1">
                <Label htmlFor="plan-roles">
                  Max Custom Roles (blank = unlimited)
                </Label>
                <Input
                  id="plan-roles"
                  type="number"
                  placeholder="Unlimited"
                  {...planForm.register("maxCustomRoles")}
                />
              </div>
              <div className="space-y-1">
                <Label htmlFor="plan-api">
                  Max Monthly API Calls (blank = unlimited)
                </Label>
                <Input
                  id="plan-api"
                  type="number"
                  placeholder="Unlimited"
                  {...planForm.register("maxMonthlyApiCalls")}
                />
              </div>
              <div className="space-y-1">
                <Label htmlFor="plan-retention">Audit Retention (days)</Label>
                <Input
                  id="plan-retention"
                  type="number"
                  {...planForm.register("auditRetentionDays")}
                />
              </div>
              <div className="space-y-1">
                <Label htmlFor="plan-trial">Trial Days</Label>
                <Input
                  id="plan-trial"
                  type="number"
                  {...planForm.register("trialDays")}
                />
              </div>
              <div className="space-y-1">
                <Label htmlFor="plan-sort">Sort Order</Label>
                <Input
                  id="plan-sort"
                  type="number"
                  {...planForm.register("sortOrder")}
                />
              </div>
            </div>

            <div className="flex flex-wrap gap-6 rounded-md border p-3 text-sm">
              <label className="flex items-center gap-2">
                <Switch
                  checked={planForm.watch("allowCustomRoles")}
                  onCheckedChange={(v) =>
                    planForm.setValue("allowCustomRoles", v)
                  }
                />
                Allow Custom Roles
              </label>
              <label className="flex items-center gap-2">
                <Switch
                  checked={planForm.watch("isActive")}
                  onCheckedChange={(v) => planForm.setValue("isActive", v)}
                />
                Active
              </label>
              <label className="flex items-center gap-2">
                <Switch
                  checked={planForm.watch("isPublic")}
                  onCheckedChange={(v) => planForm.setValue("isPublic", v)}
                />
                Publicly Listed
              </label>
            </div>

            <div className="flex justify-end gap-2">
              <Button
                variant="outline"
                onClick={() => setPlanDialogOpen(false)}
              >
                Cancel
              </Button>
              <Button type="submit" loading={savePlanMutation.isPending}>
                {editingPlan ? "Update Plan" : "Create Plan"}
              </Button>
            </div>
          </form>
        </Dialog>

        {/* Global Audit Metadata Dialog */}
        <Dialog
          open={Boolean(selectedAudit)}
          onClose={() => setSelectedAudit(null)}
          title={selectedAudit?.action ?? "Audit Log"}
          description={selectedAudit?.description}
          maxWidthClass="max-w-2xl"
        >
          {selectedAudit && (
            <pre className="max-h-72 overflow-auto rounded-md border bg-muted/40 p-3 font-mono text-xs">
              {selectedAudit.metadataJson
                ? prettyPrintJson(selectedAudit.metadataJson)
                : "No extra metadata attached."}
            </pre>
          )}
        </Dialog>
      </AppShell>
    </AuthGuard>
  );
}
