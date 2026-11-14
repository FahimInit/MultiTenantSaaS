"use client";

import * as React from "react";
import { useQuery } from "@tanstack/react-query";
import { Eye, Search } from "lucide-react";
import { auditLogsApi } from "@/lib/saas-api";
import {
  extractErrorMessage,
  formatDateTime,
  prettyPrintJson,
} from "@/lib/formatters";
import type { AuditCategory, AuditLogDto } from "@/types/api";
import { useAuthTenant } from "@/providers/AuthTenantProvider";
import { AuthGuard } from "@/components/shared/WorkspaceGuard";
import { AppShell } from "@/components/layout/AppShell";
import { PaginationControls } from "@/components/shared/PaginationControls";
import { Button } from "@/components/ui/button";
import { Input, Label, Select } from "@/components/ui/form-controls";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Alert } from "@/components/ui/alert";
import { Dialog } from "@/components/ui/dialog";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/data-display";

const CATEGORIES: AuditCategory[] = [
  "Identity",
  "Tenant",
  "Membership",
  "AccessControl",
  "Subscription",
  "Security",
  "Platform",
];

export default function AuditLogsPage() {
  const { activeTenantId, hasPermission } = useAuthTenant();

  const [page, setPage] = React.useState(1);
  const [search, setSearch] = React.useState("");
  const [category, setCategory] = React.useState<AuditCategory | "">("");
  const [action, setAction] = React.useState("");
  const [fromDate, setFromDate] = React.useState("");
  const [toDate, setToDate] = React.useState("");
  const [sortDirection, setSortDirection] = React.useState<"desc" | "asc">(
    "desc"
  );
  const [selectedLog, setSelectedLog] = React.useState<AuditLogDto | null>(
    null
  );

  const logsQuery = useQuery({
    queryKey: [
      "tenant",
      activeTenantId,
      "auditLogs",
      { page, search, category, action, fromDate, toDate, sortDirection },
    ],
    queryFn: () =>
      auditLogsApi.list({
        page,
        pageSize: 20,
        search: search || undefined,
        category,
        action: action || undefined,
        fromUtc: fromDate ? `${fromDate}T00:00:00.000Z` : undefined,
        toUtc: toDate ? `${toDate}T23:59:59.999Z` : undefined,
        sortDirection,
      }),
    enabled: Boolean(activeTenantId) && hasPermission("audit.read"),
  });

  return (
    <AuthGuard requireTenant requiredPermission="audit.read">
      <AppShell>
        <div className="space-y-6">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">
              Organization Audit & Activity Logs
            </h1>
            <p className="mt-1 text-sm text-muted-foreground">
              Tenant-scoped security trail recording member, RBAC, settings, and
              billing mutations.
            </p>
          </div>

          {logsQuery.error && (
            <Alert variant="destructive" title="Query Failed">
              {extractErrorMessage(logsQuery.error)}
            </Alert>
          )}

          <Card>
            <CardHeader>
              <CardTitle>Event Filters</CardTitle>
              <CardDescription>
                Filter by text, audit category, action code, or UTC date window.
              </CardDescription>
            </CardHeader>
            <CardContent className="grid gap-3 sm:grid-cols-2 lg:grid-cols-6">
              <div className="relative lg:col-span-2">
                <Label htmlFor="audit-search" className="sr-only">
                  Search
                </Label>
                <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  id="audit-search"
                  placeholder="Search description, actor, correlation ID..."
                  value={search}
                  onChange={(e) => {
                    setSearch(e.target.value);
                    setPage(1);
                  }}
                  className="pl-8"
                />
              </div>

              <div>
                <Label htmlFor="audit-category" className="sr-only">
                  Category
                </Label>
                <Select
                  id="audit-category"
                  value={category}
                  onChange={(e) => {
                    setCategory(e.target.value as AuditCategory | "");
                    setPage(1);
                  }}
                >
                  <option value="">All Categories</option>
                  {CATEGORIES.map((c) => (
                    <option key={c} value={c}>
                      {c}
                    </option>
                  ))}
                </Select>
              </div>

              <div>
                <Label htmlFor="audit-action" className="sr-only">
                  Action
                </Label>
                <Input
                  id="audit-action"
                  placeholder="Action code (e.g. MEMBER_ROLE_UPDATED)"
                  value={action}
                  onChange={(e) => {
                    setAction(e.target.value);
                    setPage(1);
                  }}
                />
              </div>

              <div>
                <Label htmlFor="audit-from" className="sr-only">
                  From Date
                </Label>
                <Input
                  id="audit-from"
                  type="date"
                  value={fromDate}
                  onChange={(e) => {
                    setFromDate(e.target.value);
                    setPage(1);
                  }}
                  title="From UTC date"
                />
              </div>

              <div className="flex gap-2">
                <Input
                  type="date"
                  value={toDate}
                  onChange={(e) => {
                    setToDate(e.target.value);
                    setPage(1);
                  }}
                  title="To UTC date"
                />
                <Select
                  aria-label="Sort order"
                  value={sortDirection}
                  onChange={(e) => {
                    setSortDirection(e.target.value as "asc" | "desc");
                    setPage(1);
                  }}
                  className="w-28 shrink-0"
                >
                  <option value="desc">Newest</option>
                  <option value="asc">Oldest</option>
                </Select>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="pt-5">
              {logsQuery.isLoading ? (
                <p className="py-10 text-center text-sm text-muted-foreground">
                  Querying audit events...
                </p>
              ) : logsQuery.data?.items.length === 0 ? (
                <p className="py-10 text-center text-sm text-muted-foreground">
                  No audit events match the active filters.
                </p>
              ) : (
                <>
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Timestamp</TableHead>
                        <TableHead>Category</TableHead>
                        <TableHead>Action & Description</TableHead>
                        <TableHead>Actor</TableHead>
                        <TableHead>Target</TableHead>
                        <TableHead className="text-right">Details</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {logsQuery.data?.items.map((log) => (
                        <TableRow key={log.id}>
                          <TableCell className="whitespace-nowrap text-xs text-muted-foreground">
                            {formatDateTime(log.occurredAtUtc)}
                          </TableCell>
                          <TableCell>
                            <Badge variant="outline">{log.category}</Badge>
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
                          <TableCell className="font-mono text-xs text-muted-foreground">
                            {log.targetType}: {log.targetId.slice(0, 12)}
                          </TableCell>
                          <TableCell className="text-right">
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => setSelectedLog(log)}
                              title="Inspect metadata"
                            >
                              <Eye className="h-4 w-4" />
                            </Button>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>

                  {logsQuery.data && (
                    <PaginationControls
                      page={logsQuery.data.page}
                      totalPages={logsQuery.data.totalPages}
                      totalCount={logsQuery.data.totalCount}
                      pageSize={logsQuery.data.pageSize}
                      onPageChange={setPage}
                    />
                  )}
                </>
              )}
            </CardContent>
          </Card>
        </div>

        <Dialog
          open={Boolean(selectedLog)}
          onClose={() => setSelectedLog(null)}
          title={selectedLog?.action ?? "Audit Event"}
          description={selectedLog?.description}
          maxWidthClass="max-w-2xl"
        >
          {selectedLog && (
            <div className="space-y-4 text-xs">
              <div className="grid grid-cols-2 gap-3 rounded-md border bg-muted/40 p-3">
                <div>
                  <span className="text-muted-foreground">Actor:</span>{" "}
                  <span className="font-medium">{selectedLog.actorEmail}</span>
                </div>
                <div>
                  <span className="text-muted-foreground">Timestamp:</span>{" "}
                  <span className="font-medium">
                    {formatDateTime(selectedLog.occurredAtUtc)}
                  </span>
                </div>
                <div>
                  <span className="text-muted-foreground">IP Address:</span>{" "}
                  <span className="font-mono">
                    {selectedLog.ipAddress ?? "—"}
                  </span>
                </div>
                <div>
                  <span className="text-muted-foreground">Correlation ID:</span>{" "}
                  <span className="font-mono">
                    {selectedLog.correlationId ?? "—"}
                  </span>
                </div>
                <div className="col-span-2">
                  <span className="text-muted-foreground">Target:</span>{" "}
                  <span className="font-mono">
                    {selectedLog.targetType} / {selectedLog.targetId}
                  </span>
                </div>
              </div>
              {selectedLog.metadataJson && (
                <div className="space-y-1.5">
                  <Label>Sanitized Metadata JSON</Label>
                  <pre className="max-h-64 overflow-auto rounded-md border bg-background p-3 font-mono text-xs">
                    {prettyPrintJson(selectedLog.metadataJson)}
                  </pre>
                </div>
              )}
            </div>
          )}
        </Dialog>
      </AppShell>
    </AuthGuard>
  );
}
