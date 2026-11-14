"use client";

import * as React from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import {
  MailPlus,
  RefreshCw,
  Search,
  Trash2,
  UserCheck,
  UserMinus,
  UserX,
} from "lucide-react";
import { invitationsApi, membersApi, rolesApi } from "@/lib/saas-api";
import { extractErrorMessage, formatDateTime } from "@/lib/formatters";
import type {
  InvitationStatus,
  MemberListItemDto,
  MembershipStatus,
} from "@/types/api";
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
import { Badge, statusBadgeVariant } from "@/components/ui/badge";
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

const inviteSchema = z.object({
  email: z.string().trim().email("Enter a valid email address."),
  roleId: z.string().uuid("Select a role for the invited member."),
});

type InviteFormValues = z.infer<typeof inviteSchema>;

export default function MembersPage() {
  const queryClient = useQueryClient();
  const { activeTenantId, user, hasPermission } = useAuthTenant();

  const [activeTab, setActiveTab] = React.useState<"members" | "invitations">(
    "members"
  );
  const [search, setSearch] = React.useState("");
  const [statusFilter, setStatusFilter] = React.useState<MembershipStatus | "">(
    ""
  );
  const [roleFilter, setRoleFilter] = React.useState("");
  const [page, setPage] = React.useState(1);

  const [invSearch, setInvSearch] = React.useState("");
  const [invStatus, setInvStatus] = React.useState<InvitationStatus | "">("");
  const [invPage, setInvPage] = React.useState(1);

  const [actionError, setActionError] = React.useState<string | null>(null);
  const [actionSuccess, setActionSuccess] = React.useState<string | null>(null);

  const [inviteOpen, setInviteOpen] = React.useState(false);
  const [editingMember, setEditingMember] =
    React.useState<MemberListItemDto | null>(null);
  const [selectedRoleId, setSelectedRoleId] = React.useState("");
  const [removingMember, setRemovingMember] =
    React.useState<MemberListItemDto | null>(null);

  const canInvite = hasPermission("members.invite");
  const canUpdate = hasPermission("members.update");
  const canRemove = hasPermission("members.remove");
  const canReadRoles = hasPermission("roles.read");

  const rolesQuery = useQuery({
    queryKey: ["tenant", activeTenantId, "roles"],
    queryFn: () => rolesApi.listRoles(),
    enabled: Boolean(activeTenantId) && canReadRoles,
  });

  const membersQuery = useQuery({
    queryKey: [
      "tenant",
      activeTenantId,
      "members",
      { page, search, statusFilter, roleFilter },
    ],
    queryFn: () =>
      membersApi.list({
        page,
        pageSize: 10,
        search: search || undefined,
        status: statusFilter,
        roleId: roleFilter || undefined,
      }),
    enabled: Boolean(activeTenantId) && hasPermission("members.read"),
  });

  const invitationsQuery = useQuery({
    queryKey: [
      "tenant",
      activeTenantId,
      "invitations",
      { invPage, invSearch, invStatus },
    ],
    queryFn: () =>
      invitationsApi.list({
        page: invPage,
        pageSize: 10,
        search: invSearch || undefined,
        status: invStatus,
      }),
    enabled:
      Boolean(activeTenantId) &&
      hasPermission("members.read") &&
      activeTab === "invitations",
  });

  const inviteForm = useForm<InviteFormValues>({
    resolver: zodResolver(inviteSchema),
    defaultValues: { email: "", roleId: "" },
  });

  React.useEffect(() => {
    if (rolesQuery.data && rolesQuery.data.length > 0 && !inviteForm.getValues("roleId")) {
      const defaultMemberRole =
        rolesQuery.data.find((r) => r.code === "MEMBER") ?? rolesQuery.data[0];
      inviteForm.setValue("roleId", defaultMemberRole.id);
    }
  }, [rolesQuery.data, inviteForm]);

  const invalidateAll = () => {
    void queryClient.invalidateQueries({ queryKey: ["tenant", activeTenantId] });
  };

  const inviteMutation = useMutation({
    mutationFn: (values: InviteFormValues) => invitationsApi.invite(values),
    onSuccess: (created) => {
      setActionError(null);
      setActionSuccess(`Invitation dispatched to ${created.email}.`);
      setInviteOpen(false);
      inviteForm.reset({
        email: "",
        roleId: rolesQuery.data?.[0]?.id ?? "",
      });
      setActiveTab("invitations");
      invalidateAll();
    },
    onError: (err) => setActionError(extractErrorMessage(err)),
  });

  const roleMutation = useMutation({
    mutationFn: ({ memberId, roleId }: { memberId: string; roleId: string }) =>
      membersApi.updateRole(memberId, roleId),
    onSuccess: (updated) => {
      setActionError(null);
      setActionSuccess(
        `Updated role for ${updated.fullName} to ${updated.roleName}.`
      );
      setEditingMember(null);
      invalidateAll();
    },
    onError: (err) => setActionError(extractErrorMessage(err)),
  });

  const statusMutation = useMutation({
    mutationFn: ({
      memberId,
      status,
    }: {
      memberId: string;
      status: MembershipStatus;
    }) => membersApi.updateStatus(memberId, status),
    onSuccess: (updated) => {
      setActionError(null);
      setActionSuccess(
        `Changed ${updated.fullName} membership status to ${updated.status}.`
      );
      invalidateAll();
    },
    onError: (err) => setActionError(extractErrorMessage(err)),
  });

  const removeMutation = useMutation({
    mutationFn: (memberId: string) => membersApi.remove(memberId),
    onSuccess: () => {
      setActionError(null);
      setActionSuccess("Member removed from organization.");
      setRemovingMember(null);
      invalidateAll();
    },
    onError: (err) => {
      setRemovingMember(null);
      setActionError(extractErrorMessage(err));
    },
  });

  const resendMutation = useMutation({
    mutationFn: (id: string) => invitationsApi.resend(id),
    onSuccess: (inv) => {
      setActionError(null);
      setActionSuccess(`Resent invitation to ${inv.email}.`);
      invalidateAll();
    },
    onError: (err) => setActionError(extractErrorMessage(err)),
  });

  const revokeMutation = useMutation({
    mutationFn: (id: string) => invitationsApi.revoke(id),
    onSuccess: () => {
      setActionError(null);
      setActionSuccess("Invitation revoked.");
      invalidateAll();
    },
    onError: (err) => setActionError(extractErrorMessage(err)),
  });

  return (
    <AuthGuard requireTenant requiredPermission="members.read">
      <AppShell>
        <div className="space-y-6">
          <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-center">
            <div>
              <h1 className="text-2xl font-semibold tracking-tight">
                Members & Invitations
              </h1>
              <p className="mt-1 text-sm text-muted-foreground">
                Manage workspace users, assign RBAC roles, and track pending
                invitations.
              </p>
            </div>
            {canInvite && (
              <Button onClick={() => setInviteOpen(true)}>
                <MailPlus className="h-4 w-4" />
                Invite Member
              </Button>
            )}
          </div>

          {actionError && (
            <Alert variant="destructive" title="Operation Failed">
              {actionError}
            </Alert>
          )}
          {actionSuccess && (
            <Alert variant="success" title="Updated">
              {actionSuccess}
            </Alert>
          )}

          {/* Tabs */}
          <div className="flex gap-2 border-b">
            <button
              type="button"
              onClick={() => setActiveTab("members")}
              className={`border-b-2 px-4 py-2 text-sm font-medium transition-colors ${
                activeTab === "members"
                  ? "border-primary text-foreground"
                  : "border-transparent text-muted-foreground hover:text-foreground"
              }`}
            >
              Members Directory
            </button>
            <button
              type="button"
              onClick={() => setActiveTab("invitations")}
              className={`border-b-2 px-4 py-2 text-sm font-medium transition-colors ${
                activeTab === "invitations"
                  ? "border-primary text-foreground"
                  : "border-transparent text-muted-foreground hover:text-foreground"
              }`}
            >
              Invitations
            </button>
          </div>

          {activeTab === "members" && (
            <Card>
              <CardHeader>
                <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
                  <div>
                    <CardTitle>Organization Members</CardTitle>
                    <CardDescription>
                      Filter by name, email, role, or membership status.
                    </CardDescription>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <div className="relative">
                      <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
                      <Input
                        placeholder="Search name or email..."
                        value={search}
                        onChange={(e) => {
                          setSearch(e.target.value);
                          setPage(1);
                        }}
                        className="w-56 pl-8"
                      />
                    </div>
                    <Select
                      aria-label="Filter by status"
                      value={statusFilter}
                      onChange={(e) => {
                        setStatusFilter(
                          e.target.value as MembershipStatus | ""
                        );
                        setPage(1);
                      }}
                      className="w-36"
                    >
                      <option value="">All Statuses</option>
                      <option value="Active">Active</option>
                      <option value="Suspended">Suspended</option>
                      <option value="Removed">Removed</option>
                    </Select>
                    {rolesQuery.data && (
                      <Select
                        aria-label="Filter by role"
                        value={roleFilter}
                        onChange={(e) => {
                          setRoleFilter(e.target.value);
                          setPage(1);
                        }}
                        className="w-44"
                      >
                        <option value="">All Roles</option>
                        {rolesQuery.data.map((r) => (
                          <option key={r.id} value={r.id}>
                            {r.name}
                          </option>
                        ))}
                      </Select>
                    )}
                  </div>
                </div>
              </CardHeader>
              <CardContent>
                {membersQuery.isLoading ? (
                  <p className="py-8 text-center text-sm text-muted-foreground">
                    Loading members...
                  </p>
                ) : membersQuery.data?.items.length === 0 ? (
                  <p className="py-8 text-center text-sm text-muted-foreground">
                    No matching members found.
                  </p>
                ) : (
                  <>
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Member</TableHead>
                          <TableHead>Role</TableHead>
                          <TableHead>Status</TableHead>
                          <TableHead>Joined</TableHead>
                          <TableHead>Last Sign-In</TableHead>
                          <TableHead className="text-right">Actions</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {membersQuery.data?.items.map((member) => {
                          const isSelf = member.userId === user?.id;
                          return (
                            <TableRow key={member.membershipId}>
                              <TableCell>
                                <p className="font-medium">
                                  {member.fullName}
                                  {isSelf && (
                                    <span className="ml-1.5 text-xs text-muted-foreground">
                                      (You)
                                    </span>
                                  )}
                                </p>
                                <p className="text-xs text-muted-foreground">
                                  {member.email}
                                </p>
                              </TableCell>
                              <TableCell>
                                <Badge variant="outline">
                                  {member.roleName}
                                </Badge>
                              </TableCell>
                              <TableCell>
                                <Badge
                                  variant={statusBadgeVariant(member.status)}
                                >
                                  {member.status}
                                </Badge>
                              </TableCell>
                              <TableCell className="text-xs text-muted-foreground">
                                {formatDateTime(member.joinedAtUtc)}
                              </TableCell>
                              <TableCell className="text-xs text-muted-foreground">
                                {formatDateTime(member.lastLoginAtUtc)}
                              </TableCell>
                              <TableCell className="text-right">
                                <div className="flex justify-end gap-1.5">
                                  {canUpdate &&
                                    member.status !== "Removed" && (
                                      <Button
                                        variant="outline"
                                        size="sm"
                                        onClick={() => {
                                          setEditingMember(member);
                                          setSelectedRoleId(member.roleId);
                                        }}
                                      >
                                        Role
                                      </Button>
                                    )}
                                  {canUpdate &&
                                    !isSelf &&
                                    member.status === "Active" && (
                                      <Button
                                        variant="outline"
                                        size="sm"
                                        onClick={() =>
                                          statusMutation.mutate({
                                            memberId: member.membershipId,
                                            status: "Suspended",
                                          })
                                        }
                                        title="Suspend member"
                                      >
                                        <UserX className="h-3.5 w-3.5" />
                                      </Button>
                                    )}
                                  {canUpdate &&
                                    member.status === "Suspended" && (
                                      <Button
                                        variant="outline"
                                        size="sm"
                                        onClick={() =>
                                          statusMutation.mutate({
                                            memberId: member.membershipId,
                                            status: "Active",
                                          })
                                        }
                                        title="Reactivate member"
                                      >
                                        <UserCheck className="h-3.5 w-3.5" />
                                      </Button>
                                    )}
                                  {canRemove &&
                                    !isSelf &&
                                    member.status !== "Removed" && (
                                      <Button
                                        variant="destructive"
                                        size="sm"
                                        onClick={() =>
                                          setRemovingMember(member)
                                        }
                                        title="Remove member"
                                      >
                                        <UserMinus className="h-3.5 w-3.5" />
                                      </Button>
                                    )}
                                </div>
                              </TableCell>
                            </TableRow>
                          );
                        })}
                      </TableBody>
                    </Table>
                    {membersQuery.data && (
                      <PaginationControls
                        page={membersQuery.data.page}
                        totalPages={membersQuery.data.totalPages}
                        totalCount={membersQuery.data.totalCount}
                        pageSize={membersQuery.data.pageSize}
                        onPageChange={setPage}
                      />
                    )}
                  </>
                )}
              </CardContent>
            </Card>
          )}

          {activeTab === "invitations" && (
            <Card>
              <CardHeader>
                <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
                  <div>
                    <CardTitle>Workspace Invitations</CardTitle>
                    <CardDescription>
                      Resend or revoke pending invitations before acceptance.
                    </CardDescription>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Input
                      placeholder="Filter invited email..."
                      value={invSearch}
                      onChange={(e) => {
                        setInvSearch(e.target.value);
                        setInvPage(1);
                      }}
                      className="w-52"
                    />
                    <Select
                      aria-label="Filter invitations by status"
                      value={invStatus}
                      onChange={(e) => {
                        setInvStatus(
                          e.target.value as InvitationStatus | ""
                        );
                        setInvPage(1);
                      }}
                      className="w-36"
                    >
                      <option value="">All Statuses</option>
                      <option value="Pending">Pending</option>
                      <option value="Accepted">Accepted</option>
                      <option value="Revoked">Revoked</option>
                      <option value="Expired">Expired</option>
                    </Select>
                  </div>
                </div>
              </CardHeader>
              <CardContent>
                {invitationsQuery.isLoading ? (
                  <p className="py-8 text-center text-sm text-muted-foreground">
                    Loading invitations...
                  </p>
                ) : invitationsQuery.data?.items.length === 0 ? (
                  <p className="py-8 text-center text-sm text-muted-foreground">
                    No invitations match the selected filters.
                  </p>
                ) : (
                  <>
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Invitee</TableHead>
                          <TableHead>Assigned Role</TableHead>
                          <TableHead>Status</TableHead>
                          <TableHead>Invited By</TableHead>
                          <TableHead>Expires</TableHead>
                          <TableHead className="text-right">Actions</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {invitationsQuery.data?.items.map((inv) => (
                          <TableRow key={inv.id}>
                            <TableCell className="font-medium">
                              {inv.email}
                            </TableCell>
                            <TableCell>
                              <Badge variant="outline">{inv.roleName}</Badge>
                            </TableCell>
                            <TableCell>
                              <Badge variant={statusBadgeVariant(inv.status)}>
                                {inv.status}
                              </Badge>
                            </TableCell>
                            <TableCell className="text-xs text-muted-foreground">
                              {inv.invitedByEmail}
                            </TableCell>
                            <TableCell className="text-xs text-muted-foreground">
                              {formatDateTime(inv.expiresAtUtc)}
                            </TableCell>
                            <TableCell className="text-right">
                              {canInvite && inv.status === "Pending" && (
                                <div className="flex justify-end gap-1.5">
                                  <Button
                                    variant="outline"
                                    size="sm"
                                    onClick={() => resendMutation.mutate(inv.id)}
                                    loading={resendMutation.isPending}
                                    title="Resend invitation"
                                  >
                                    <RefreshCw className="h-3.5 w-3.5" />
                                  </Button>
                                  <Button
                                    variant="destructive"
                                    size="sm"
                                    onClick={() => revokeMutation.mutate(inv.id)}
                                    loading={revokeMutation.isPending}
                                    title="Revoke invitation"
                                  >
                                    <Trash2 className="h-3.5 w-3.5" />
                                  </Button>
                                </div>
                              )}
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                    {invitationsQuery.data && (
                      <PaginationControls
                        page={invitationsQuery.data.page}
                        totalPages={invitationsQuery.data.totalPages}
                        totalCount={invitationsQuery.data.totalCount}
                        pageSize={invitationsQuery.data.pageSize}
                        onPageChange={setInvPage}
                      />
                    )}
                  </>
                )}
              </CardContent>
            </Card>
          )}
        </div>

        {/* Invite Member Dialog */}
        <Dialog
          open={inviteOpen}
          onClose={() => setInviteOpen(false)}
          title="Invite Team Member"
          description="Send an organization invitation to an email address with an initial RBAC role."
        >
          <form
            onSubmit={inviteForm.handleSubmit((v) => inviteMutation.mutate(v))}
            className="space-y-4"
          >
            <div className="space-y-1.5">
              <Label htmlFor="invite-email">Email Address</Label>
              <Input
                id="invite-email"
                type="email"
                placeholder="colleague@company.com"
                {...inviteForm.register("email")}
              />
              {inviteForm.formState.errors.email && (
                <p className="text-xs text-destructive">
                  {inviteForm.formState.errors.email.message}
                </p>
              )}
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="invite-role">Role</Label>
              <Select id="invite-role" {...inviteForm.register("roleId")}>
                <option value="">Select a role...</option>
                {rolesQuery.data?.map((r) => (
                  <option key={r.id} value={r.id}>
                    {r.name} ({r.code})
                  </option>
                ))}
              </Select>
              {inviteForm.formState.errors.roleId && (
                <p className="text-xs text-destructive">
                  {inviteForm.formState.errors.roleId.message}
                </p>
              )}
            </div>

            <div className="flex justify-end gap-2 pt-2">
              <Button variant="outline" onClick={() => setInviteOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" loading={inviteMutation.isPending}>
                Send Invitation
              </Button>
            </div>
          </form>
        </Dialog>

        {/* Change Role Dialog */}
        <Dialog
          open={Boolean(editingMember)}
          onClose={() => setEditingMember(null)}
          title={`Update Role — ${editingMember?.fullName ?? ""}`}
          description="Reassign workspace permissions immediately (cache invalidated automatically)."
        >
          <div className="space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="member-role-select">Assigned Role</Label>
              <Select
                id="member-role-select"
                value={selectedRoleId}
                onChange={(e) => setSelectedRoleId(e.target.value)}
              >
                {rolesQuery.data?.map((r) => (
                  <option key={r.id} value={r.id}>
                    {r.name} ({r.code})
                  </option>
                ))}
              </Select>
            </div>
            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={() => setEditingMember(null)}>
                Cancel
              </Button>
              <Button
                loading={roleMutation.isPending}
                onClick={() => {
                  if (editingMember && selectedRoleId) {
                    roleMutation.mutate({
                      memberId: editingMember.membershipId,
                      roleId: selectedRoleId,
                    });
                  }
                }}
              >
                Save Role
              </Button>
            </div>
          </div>
        </Dialog>

        {/* Confirm Removal Dialog */}
        <Dialog
          open={Boolean(removingMember)}
          onClose={() => setRemovingMember(null)}
          title="Remove Member"
          description={`Are you sure you want to remove ${removingMember?.fullName} (${removingMember?.email}) from this organization?`}
        >
          <div className="flex justify-end gap-2 pt-2">
            <Button variant="outline" onClick={() => setRemovingMember(null)}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              loading={removeMutation.isPending}
              onClick={() => {
                if (removingMember) {
                  removeMutation.mutate(removingMember.membershipId);
                }
              }}
            >
              Confirm Removal
            </Button>
          </div>
        </Dialog>
      </AppShell>
    </AuthGuard>
  );
}
