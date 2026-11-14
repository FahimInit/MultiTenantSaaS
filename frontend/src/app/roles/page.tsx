"use client";

import * as React from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Edit2, Lock, Plus, Shield, Trash2 } from "lucide-react";
import { rolesApi, subscriptionsApi } from "@/lib/saas-api";
import { extractErrorMessage } from "@/lib/formatters";
import type {
  PermissionCatalogItemDto,
  TenantRoleSummaryDto,
} from "@/types/api";
import { useAuthTenant } from "@/providers/AuthTenantProvider";
import { AuthGuard } from "@/components/shared/WorkspaceGuard";
import { AppShell } from "@/components/layout/AppShell";
import { Button } from "@/components/ui/button";
import { Input, Label, Textarea } from "@/components/ui/form-controls";
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

const roleSchema = z.object({
  name: z
    .string()
    .trim()
    .min(2, "Role name must be at least 2 characters.")
    .max(80),
  description: z.string().trim().max(300),
  permissions: z
    .array(z.string())
    .min(1, "Select at least one permission capability."),
});

type RoleFormValues = z.infer<typeof roleSchema>;

export default function RolesPage() {
  const queryClient = useQueryClient();
  const { activeTenantId, hasPermission } = useAuthTenant();
  const canManage = hasPermission("roles.manage");
  const canReadBilling = hasPermission("billing.read");

  const [errorMsg, setErrorMsg] = React.useState<string | null>(null);
  const [successMsg, setSuccessMsg] = React.useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = React.useState(false);
  const [editingRole, setEditingRole] =
    React.useState<TenantRoleSummaryDto | null>(null);
  const [deletingRole, setDeletingRole] =
    React.useState<TenantRoleSummaryDto | null>(null);

  const rolesQuery = useQuery({
    queryKey: ["tenant", activeTenantId, "roles"],
    queryFn: () => rolesApi.listRoles(),
    enabled: Boolean(activeTenantId) && hasPermission("roles.read"),
  });

  const permissionsQuery = useQuery({
    queryKey: ["tenant", activeTenantId, "permissions"],
    queryFn: () => rolesApi.listPermissions(),
    enabled: Boolean(activeTenantId) && hasPermission("roles.read"),
  });

  const subscriptionQuery = useQuery({
    queryKey: ["tenant", activeTenantId, "subscription"],
    queryFn: () => subscriptionsApi.getCurrent(),
    enabled: Boolean(activeTenantId) && canReadBilling,
  });

  const form = useForm<RoleFormValues>({
    resolver: zodResolver(roleSchema),
    defaultValues: { name: "", description: "", permissions: [] },
  });

  const selectedPermissions = form.watch("permissions");

  const groupedPermissions = React.useMemo(() => {
    const map = new Map<string, PermissionCatalogItemDto[]>();
    for (const item of permissionsQuery.data ?? []) {
      const existing = map.get(item.module) ?? [];
      existing.push(item);
      map.set(item.module, existing);
    }
    return Array.from(map.entries());
  }, [permissionsQuery.data]);

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ["tenant", activeTenantId] });
  };

  const upsertMutation = useMutation({
    mutationFn: (values: RoleFormValues) =>
      editingRole
        ? rolesApi.updateRole(editingRole.id, values)
        : rolesApi.createRole(values),
    onSuccess: (saved) => {
      setErrorMsg(null);
      setSuccessMsg(
        editingRole
          ? `Updated role "${saved.name}".`
          : `Created custom role "${saved.name}".`
      );
      setDialogOpen(false);
      setEditingRole(null);
      invalidate();
    },
    onError: (err) => setErrorMsg(extractErrorMessage(err)),
  });

  const deleteMutation = useMutation({
    mutationFn: (roleId: string) => rolesApi.deleteRole(roleId),
    onSuccess: () => {
      setErrorMsg(null);
      setSuccessMsg("Custom role deleted.");
      setDeletingRole(null);
      invalidate();
    },
    onError: (err) => {
      setDeletingRole(null);
      setErrorMsg(extractErrorMessage(err));
    },
  });

  const openCreateModal = () => {
    setErrorMsg(null);
    setEditingRole(null);
    form.reset({
      name: "",
      description: "",
      permissions: ["dashboard.read"],
    });
    setDialogOpen(true);
  };

  const openEditModal = (role: TenantRoleSummaryDto) => {
    setErrorMsg(null);
    setEditingRole(role);
    form.reset({
      name: role.name,
      description: role.description,
      permissions: [...role.permissions],
    });
    setDialogOpen(true);
  };

  const togglePermission = (code: string) => {
    const current = new Set(form.getValues("permissions"));
    if (current.has(code)) {
      current.delete(code);
    } else {
      current.add(code);
    }
    form.setValue("permissions", Array.from(current), { shouldValidate: true });
  };

  const allowCustomRoles =
    subscriptionQuery.data?.plan.allowCustomRoles ?? true;

  return (
    <AuthGuard requireTenant requiredPermission="roles.read">
      <AppShell>
        <div className="space-y-6">
          <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-center">
            <div>
              <h1 className="text-2xl font-semibold tracking-tight">
                Roles & Permissions (RBAC)
              </h1>
              <p className="mt-1 text-sm text-muted-foreground">
                Inspect built-in workspace roles and configure capability-based
                custom roles.
              </p>
            </div>
            {canManage && (
              <Button onClick={openCreateModal}>
                <Plus className="h-4 w-4" />
                Create Custom Role
              </Button>
            )}
          </div>

          {!allowCustomRoles && canReadBilling && (
            <Alert variant="warning" title="Custom Roles Restricted on Current Plan">
              Your current plan ({subscriptionQuery.data?.plan.name}) does not
              permit custom roles. Upgrade to a paid plan on the Subscription
              page to unlock custom RBAC roles.
            </Alert>
          )}

          {errorMsg && (
            <Alert variant="destructive" title="Role Update Error">
              {errorMsg}
            </Alert>
          )}
          {successMsg && (
            <Alert variant="success" title="Success">
              {successMsg}
            </Alert>
          )}

          {rolesQuery.isLoading ? (
            <p className="py-10 text-center text-sm text-muted-foreground">
              Loading roles and capabilities...
            </p>
          ) : (
            <div className="grid gap-4 md:grid-cols-2">
              {rolesQuery.data?.map((role) => (
                <Card key={role.id}>
                  <CardHeader className="pb-3">
                    <div className="flex items-start justify-between gap-2">
                      <div className="space-y-1">
                        <CardTitle className="flex items-center gap-2 text-base">
                          <Shield className="h-4 w-4 text-muted-foreground" />
                          {role.name}
                          {role.isSystemRole ? (
                            <Badge variant="secondary">
                              <Lock className="h-3 w-3" />
                              System
                            </Badge>
                          ) : (
                            <Badge variant="info">Custom</Badge>
                          )}
                        </CardTitle>
                        <CardDescription className="text-xs">
                          Code: <code>{role.code}</code> · {role.memberCount}{" "}
                          assigned member(s)
                        </CardDescription>
                      </div>
                      {canManage && !role.isSystemRole && (
                        <div className="flex gap-1">
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => openEditModal(role)}
                            title="Edit custom role"
                          >
                            <Edit2 className="h-3.5 w-3.5" />
                          </Button>
                          <Button
                            variant="destructive"
                            size="sm"
                            onClick={() => setDeletingRole(role)}
                            title="Delete custom role"
                          >
                            <Trash2 className="h-3.5 w-3.5" />
                          </Button>
                        </div>
                      )}
                    </div>
                  </CardHeader>
                  <CardContent className="space-y-3">
                    <p className="text-sm text-muted-foreground">
                      {role.description || "No description provided."}
                    </p>
                    <div className="flex flex-wrap gap-1.5">
                      {role.permissions.map((perm) => (
                        <Badge
                          key={perm}
                          variant="outline"
                          className="font-mono text-[11px]"
                        >
                          {perm}
                        </Badge>
                      ))}
                    </div>
                  </CardContent>
                </Card>
              ))}
            </div>
          )}
        </div>

        {/* Create / Edit Role Modal */}
        <Dialog
          open={dialogOpen}
          onClose={() => setDialogOpen(false)}
          title={editingRole ? `Edit Role: ${editingRole.name}` : "Create Custom Role"}
          description="Select explicit permission capabilities within your organization."
          maxWidthClass="max-w-2xl"
        >
          <form
            onSubmit={form.handleSubmit((v) => upsertMutation.mutate(v))}
            className="space-y-4"
          >
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label htmlFor="role-name">Role Name</Label>
                <Input
                  id="role-name"
                  placeholder="Security Auditor"
                  {...form.register("name")}
                />
                {form.formState.errors.name && (
                  <p className="text-xs text-destructive">
                    {form.formState.errors.name.message}
                  </p>
                )}
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="role-desc">Description</Label>
                <Textarea
                  id="role-desc"
                  className="min-h-[38px]"
                  placeholder="Read-only compliance and billing access"
                  {...form.register("description")}
                />
              </div>
            </div>

            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <Label>Capabilities ({selectedPermissions.length} selected)</Label>
                {form.formState.errors.permissions && (
                  <p className="text-xs text-destructive">
                    {form.formState.errors.permissions.message}
                  </p>
                )}
              </div>
              <div className="grid max-h-72 gap-3 overflow-y-auto rounded-md border p-3 sm:grid-cols-2">
                {groupedPermissions.map(([moduleName, items]) => (
                  <div key={moduleName} className="space-y-1.5">
                    <p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                      {moduleName}
                    </p>
                    {items.map((perm) => {
                      const checked = selectedPermissions.includes(perm.code);
                      return (
                        <label
                          key={perm.code}
                          className="flex cursor-pointer items-start gap-2 rounded p-1 text-xs hover:bg-muted/60"
                        >
                          <input
                            type="checkbox"
                            checked={checked}
                            onChange={() => togglePermission(perm.code)}
                            className="mt-0.5 rounded border-input"
                          />
                          <div>
                            <p className="font-mono font-medium">{perm.code}</p>
                            <p className="text-[11px] text-muted-foreground">
                              {perm.description}
                            </p>
                          </div>
                        </label>
                      );
                    })}
                  </div>
                ))}
              </div>
            </div>

            <div className="flex justify-end gap-2 pt-2">
              <Button variant="outline" onClick={() => setDialogOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" loading={upsertMutation.isPending}>
                {editingRole ? "Save Changes" : "Create Role"}
              </Button>
            </div>
          </form>
        </Dialog>

        {/* Confirm Role Deletion */}
        <Dialog
          open={Boolean(deletingRole)}
          onClose={() => setDeletingRole(null)}
          title="Delete Custom Role"
          description={`Confirm deleting "${deletingRole?.name}". Roles assigned to members or pending invitations cannot be deleted until reassigned.`}
        >
          <div className="flex justify-end gap-2 pt-2">
            <Button variant="outline" onClick={() => setDeletingRole(null)}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              loading={deleteMutation.isPending}
              onClick={() => {
                if (deletingRole) deleteMutation.mutate(deletingRole.id);
              }}
            >
              Delete Role
            </Button>
          </div>
        </Dialog>
      </AppShell>
    </AuthGuard>
  );
}
