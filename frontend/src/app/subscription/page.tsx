"use client";

import * as React from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Check, CreditCard, History, ShieldAlert } from "lucide-react";
import { subscriptionsApi } from "@/lib/saas-api";
import {
  extractErrorMessage,
  formatCurrencyCents,
  formatDateShort,
  formatDateTime,
  formatNumber,
} from "@/lib/formatters";
import type { BillingCycle, SubscriptionPlanDto } from "@/types/api";
import { useAuthTenant } from "@/providers/AuthTenantProvider";
import { AuthGuard } from "@/components/shared/WorkspaceGuard";
import { AppShell } from "@/components/layout/AppShell";
import { PaginationControls } from "@/components/shared/PaginationControls";
import { Button } from "@/components/ui/button";
import { Input, Label, Textarea } from "@/components/ui/form-controls";
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Badge, statusBadgeVariant } from "@/components/ui/badge";
import { Alert } from "@/components/ui/alert";
import { Dialog } from "@/components/ui/dialog";
import {
  ProgressBar,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/data-display";

export default function SubscriptionPage() {
  const queryClient = useQueryClient();
  const { activeTenantId, hasPermission } = useAuthTenant();
  const canManage = hasPermission("billing.manage");

  const [cycle, setCycle] = React.useState<BillingCycle>("Monthly");
  const [historyPage, setHistoryPage] = React.useState(1);
  const [errorMsg, setErrorMsg] = React.useState<string | null>(null);
  const [successMsg, setSuccessMsg] = React.useState<string | null>(null);

  const [selectedPlan, setSelectedPlan] =
    React.useState<SubscriptionPlanDto | null>(null);
  const [paymentToken, setPaymentToken] = React.useState("tok_visa_sandbox");
  const [cancelOpen, setCancelOpen] = React.useState(false);
  const [cancelReason, setCancelReason] = React.useState("");

  const currentQuery = useQuery({
    queryKey: ["tenant", activeTenantId, "subscription"],
    queryFn: () => subscriptionsApi.getCurrent(),
    enabled: Boolean(activeTenantId) && hasPermission("billing.read"),
  });

  const plansQuery = useQuery({
    queryKey: ["publicPlans"],
    queryFn: () => subscriptionsApi.listPublicPlans(),
  });

  const historyQuery = useQuery({
    queryKey: ["tenant", activeTenantId, "subscriptionHistory", historyPage],
    queryFn: () =>
      subscriptionsApi.getHistory({ page: historyPage, pageSize: 10 }),
    enabled: Boolean(activeTenantId) && hasPermission("billing.read"),
  });

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ["tenant", activeTenantId] });
  };

  const changePlanMutation = useMutation({
    mutationFn: (payload: {
      planCode: string;
      billingCycle: BillingCycle;
      paymentMethodToken?: string | null;
    }) => subscriptionsApi.changePlan(payload),
    onSuccess: (updated) => {
      setErrorMsg(null);
      setSuccessMsg(
        `Subscription updated to ${updated.plan.name} (${updated.billingCycle}).`
      );
      setSelectedPlan(null);
      invalidate();
    },
    onError: (err) => setErrorMsg(extractErrorMessage(err)),
  });

  const cancelMutation = useMutation({
    mutationFn: (reason: string) => subscriptionsApi.cancel(reason),
    onSuccess: () => {
      setErrorMsg(null);
      setSuccessMsg("Subscription canceled.");
      setCancelOpen(false);
      setCancelReason("");
      invalidate();
    },
    onError: (err) => setErrorMsg(extractErrorMessage(err)),
  });

  const current = currentQuery.data;

  return (
    <AuthGuard requireTenant requiredPermission="billing.read">
      <AppShell>
        <div className="space-y-6">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">
              Subscription & Quota Usage
            </h1>
            <p className="mt-1 text-sm text-muted-foreground">
              Compare plans, monitor quota headroom, and inspect billing ledger
              history.
            </p>
          </div>

          {errorMsg && (
            <Alert variant="destructive" title="Billing Action Failed">
              {errorMsg}
            </Alert>
          )}
          {successMsg && (
            <Alert variant="success" title="Subscription Updated">
              {successMsg}
            </Alert>
          )}

          {current && (
            <Card>
              <CardHeader>
                <div className="flex flex-wrap items-start justify-between gap-4">
                  <div>
                    <div className="flex items-center gap-2">
                      <CardTitle className="text-lg">
                        Current Plan: {current.plan.name}
                      </CardTitle>
                      <Badge variant={statusBadgeVariant(current.status)}>
                        {current.status}
                      </Badge>
                    </div>
                    <CardDescription className="mt-1">
                      Billing cycle: {current.billingCycle} · Current period ends{" "}
                      {formatDateShort(current.currentPeriodEndUtc)} (
                      {current.daysRemaining} days left)
                    </CardDescription>
                  </div>
                  {canManage && current.status !== "Canceled" && current.plan.code !== "FREE" && (
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => setCancelOpen(true)}
                    >
                      Cancel Subscription
                    </Button>
                  )}
                </div>
              </CardHeader>
              <CardContent className="space-y-4">
                {(!current.allowsWorkspaceWrites || current.isTrialExpired) && (
                  <Alert variant="destructive" title="Workspace Write Lock Active">
                    <span className="flex items-center gap-1.5">
                      <ShieldAlert className="h-4 w-4 shrink-0" />
                      Your subscription is inactive or its trial has expired.
                      Select a plan below to restore full write capabilities.
                    </span>
                  </Alert>
                )}

                <div className="grid gap-4 md:grid-cols-3">
                  <div className="space-y-1.5 rounded-md border p-3">
                    <p className="text-xs text-muted-foreground">Active Seats</p>
                    <p className="text-sm font-semibold">
                      {current.usage.activeMembersUsed} /{" "}
                      {current.usage.maxMembers ?? "Unlimited"}
                    </p>
                    <ProgressBar
                      percent={current.usage.membersUtilizationPercent ?? 0}
                    />
                  </div>
                  <div className="space-y-1.5 rounded-md border p-3">
                    <p className="text-xs text-muted-foreground">Custom Roles</p>
                    <p className="text-sm font-semibold">
                      {current.usage.customRolesUsed} /{" "}
                      {current.usage.maxCustomRoles ??
                        (current.plan.allowCustomRoles ? "Unlimited" : "0")}
                    </p>
                    <ProgressBar
                      percent={current.usage.customRolesUtilizationPercent ?? 0}
                    />
                  </div>
                  <div className="space-y-1.5 rounded-md border p-3">
                    <p className="text-xs text-muted-foreground">
                      Monthly API Requests
                    </p>
                    <p className="text-sm font-semibold">
                      {formatNumber(current.usage.monthlyApiCallsUsed)} /{" "}
                      {current.usage.maxMonthlyApiCalls === null
                        ? "Unlimited"
                        : formatNumber(current.usage.maxMonthlyApiCalls)}
                    </p>
                    <ProgressBar
                      percent={current.usage.apiCallsUtilizationPercent ?? 0}
                    />
                  </div>
                </div>
              </CardContent>
            </Card>
          )}

          {/* Available Plans */}
          <div className="space-y-4">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <h2 className="text-lg font-semibold">Available Plans</h2>
              <div className="inline-flex rounded-md border bg-card p-1 text-xs">
                <button
                  type="button"
                  onClick={() => setCycle("Monthly")}
                  className={`rounded px-3 py-1 font-medium transition-colors ${
                    cycle === "Monthly"
                      ? "bg-primary text-primary-foreground"
                      : "text-muted-foreground hover:text-foreground"
                  }`}
                >
                  Monthly
                </button>
                <button
                  type="button"
                  onClick={() => setCycle("Annual")}
                  className={`rounded px-3 py-1 font-medium transition-colors ${
                    cycle === "Annual"
                      ? "bg-primary text-primary-foreground"
                      : "text-muted-foreground hover:text-foreground"
                  }`}
                >
                  Annual Billing
                </button>
              </div>
            </div>

            <div className="grid gap-4 md:grid-cols-3">
              {plansQuery.data?.map((plan) => {
                const isCurrent =
                  current?.plan.code === plan.code &&
                  current?.status !== "Canceled" &&
                  current?.status !== "Expired";
                const priceCents =
                  cycle === "Monthly"
                    ? plan.monthlyPriceCents
                    : plan.annualPriceCents;
                return (
                  <Card
                    key={plan.id}
                    className={
                      isCurrent ? "border-primary ring-1 ring-primary/20" : ""
                    }
                  >
                    <CardHeader>
                      <div className="flex items-center justify-between">
                        <CardTitle>{plan.name}</CardTitle>
                        {isCurrent && <Badge variant="success">Current</Badge>}
                      </div>
                      <CardDescription>{plan.description}</CardDescription>
                    </CardHeader>
                    <CardContent className="space-y-3">
                      <div>
                        <span className="text-3xl font-bold">
                          {formatCurrencyCents(priceCents)}
                        </span>
                        <span className="text-sm text-muted-foreground">
                          /{cycle === "Monthly" ? "mo" : "yr"}
                        </span>
                      </div>
                      <ul className="space-y-2 text-xs text-muted-foreground">
                        <li className="flex items-center gap-2">
                          <Check className="h-3.5 w-3.5 text-emerald-500" />
                          Members:{" "}
                          {plan.maxMembers === null
                            ? "Unlimited"
                            : `Up to ${plan.maxMembers}`}
                        </li>
                        <li className="flex items-center gap-2">
                          <Check className="h-3.5 w-3.5 text-emerald-500" />
                          Custom Roles:{" "}
                          {!plan.allowCustomRoles
                            ? "Not included"
                            : plan.maxCustomRoles === null
                              ? "Unlimited"
                              : `Up to ${plan.maxCustomRoles}`}
                        </li>
                        <li className="flex items-center gap-2">
                          <Check className="h-3.5 w-3.5 text-emerald-500" />
                          API Quota:{" "}
                          {plan.maxMonthlyApiCalls === null
                            ? "Unlimited"
                            : `${formatNumber(plan.maxMonthlyApiCalls)} / mo`}
                        </li>
                        <li className="flex items-center gap-2">
                          <Check className="h-3.5 w-3.5 text-emerald-500" />
                          Audit Retention: {plan.auditRetentionDays} days
                        </li>
                      </ul>
                    </CardContent>
                    <CardFooter>
                      {canManage && (
                        <Button
                          className="w-full"
                          variant={isCurrent ? "outline" : "default"}
                          disabled={isCurrent && current?.billingCycle === cycle}
                          onClick={() => setSelectedPlan(plan)}
                        >
                          {isCurrent && current?.billingCycle === cycle
                            ? "Active Plan"
                            : `Switch to ${plan.name}`}
                        </Button>
                      )}
                    </CardFooter>
                  </Card>
                );
              })}
            </div>
          </div>

          {/* Billing History */}
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <History className="h-4 w-4" />
                Subscription Ledger & History
              </CardTitle>
              <CardDescription>
                Immutable record of plan changes, renewals, and mock payment
                references.
              </CardDescription>
            </CardHeader>
            <CardContent>
              {historyQuery.data?.items.length === 0 ? (
                <p className="py-6 text-center text-sm text-muted-foreground">
                  No subscription events recorded yet.
                </p>
              ) : (
                <>
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Event</TableHead>
                        <TableHead>Plan Transition</TableHead>
                        <TableHead>Amount</TableHead>
                        <TableHead>Payment Ref</TableHead>
                        <TableHead>Date</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {historyQuery.data?.items.map((item) => (
                        <TableRow key={item.id}>
                          <TableCell>
                            <Badge variant="outline">{item.eventType}</Badge>
                          </TableCell>
                          <TableCell className="text-sm">
                            {item.fromPlanName
                              ? `${item.fromPlanName} -> ${item.toPlanName}`
                              : item.toPlanName}
                            {item.notes && (
                              <p className="text-xs text-muted-foreground">
                                {item.notes}
                              </p>
                            )}
                          </TableCell>
                          <TableCell className="font-mono text-xs">
                            {formatCurrencyCents(item.amountCents, item.currency)}
                          </TableCell>
                          <TableCell className="font-mono text-xs text-muted-foreground">
                            {item.paymentReference ?? "—"}
                          </TableCell>
                          <TableCell className="text-xs text-muted-foreground">
                            {formatDateTime(item.occurredAtUtc)}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                  {historyQuery.data && (
                    <PaginationControls
                      page={historyQuery.data.page}
                      totalPages={historyQuery.data.totalPages}
                      totalCount={historyQuery.data.totalCount}
                      pageSize={historyQuery.data.pageSize}
                      onPageChange={setHistoryPage}
                    />
                  )}
                </>
              )}
            </CardContent>
          </Card>
        </div>

        {/* Confirm Plan Change Dialog (MockPaymentProvider) */}
        <Dialog
          open={Boolean(selectedPlan)}
          onClose={() => setSelectedPlan(null)}
          title={`Switch Plan to ${selectedPlan?.name ?? ""}`}
          description="Processed via MockPaymentProvider abstraction."
        >
          {selectedPlan && (
            <div className="space-y-4">
              <div className="rounded-md border bg-muted/40 p-3 text-sm">
                <p>
                  Cycle: <strong>{cycle}</strong>
                </p>
                <p>
                  Charge Amount:{" "}
                  <strong>
                    {formatCurrencyCents(
                      cycle === "Monthly"
                        ? selectedPlan.monthlyPriceCents
                        : selectedPlan.annualPriceCents
                    )}
                  </strong>
                </p>
              </div>
              {selectedPlan.monthlyPriceCents > 0 && (
                <div className="space-y-1.5">
                  <Label htmlFor="payment-token">
                    Mock Payment Method Token
                  </Label>
                  <Input
                    id="payment-token"
                    value={paymentToken}
                    onChange={(e) => setPaymentToken(e.target.value)}
                    className="font-mono text-xs"
                    placeholder="tok_visa_sandbox"
                  />
                  <p className="text-xs text-muted-foreground">
                    Use any non-empty sandbox token (e.g., <code>tok_visa</code>
                    ). Passing <code>tok_declined</code> simulates card decline.
                  </p>
                </div>
              )}
              <div className="flex justify-end gap-2">
                <Button
                  variant="outline"
                  onClick={() => setSelectedPlan(null)}
                >
                  Cancel
                </Button>
                <Button
                  loading={changePlanMutation.isPending}
                  onClick={() =>
                    changePlanMutation.mutate({
                      planCode: selectedPlan.code,
                      billingCycle: cycle,
                      paymentMethodToken:
                        selectedPlan.monthlyPriceCents > 0
                          ? paymentToken
                          : null,
                    })
                  }
                >
                  <CreditCard className="h-4 w-4" />
                  Confirm Plan Change
                </Button>
              </div>
            </div>
          )}
        </Dialog>

        {/* Cancel Subscription Dialog */}
        <Dialog
          open={cancelOpen}
          onClose={() => setCancelOpen(false)}
          title="Cancel Subscription"
          description="Canceling marks your subscription as Canceled and may restrict write operations after downgrade."
        >
          <div className="space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="cancel-reason">Cancellation Reason (Optional)</Label>
              <Textarea
                id="cancel-reason"
                value={cancelReason}
                onChange={(e) => setCancelReason(e.target.value)}
                placeholder="No longer needed for this project..."
              />
            </div>
            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={() => setCancelOpen(false)}>
                Keep Subscription
              </Button>
              <Button
                variant="destructive"
                loading={cancelMutation.isPending}
                onClick={() => cancelMutation.mutate(cancelReason)}
              >
                Confirm Cancellation
              </Button>
            </div>
          </div>
        </Dialog>
      </AppShell>
    </AuthGuard>
  );
}
