"use client";

import * as React from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Building2, Laptop, Save, ShieldCheck, Trash2 } from "lucide-react";
import { authApi, tenantsApi } from "@/lib/saas-api";
import { extractErrorMessage, formatDateTime } from "@/lib/formatters";
import { useAuthTenant } from "@/providers/AuthTenantProvider";
import { AuthGuard } from "@/components/shared/WorkspaceGuard";
import { AppShell } from "@/components/layout/AppShell";
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
import {
  Switch,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/data-display";

const profileSchema = z.object({
  name: z.string().trim().min(2).max(120),
  slug: z
    .string()
    .trim()
    .min(3)
    .max(63)
    .regex(/^[a-z0-9]+(?:-[a-z0-9]+)*$/, "Lowercase letters, digits, hyphens only."),
});

const settingsSchema = z.object({
  billingEmail: z.string().trim().email("Enter a valid billing email."),
  supportEmail: z
    .string()
    .trim()
    .email("Enter a valid support email.")
    .or(z.literal("")),
  timezone: z.string().trim().min(1, "Timezone is required."),
  locale: z.string().trim().min(2, "Locale is required."),
  enforceEmailDomain: z.boolean(),
  allowedDomainsText: z.string(),
  sessionTimeoutMinutes: z.coerce
    .number()
    .int()
    .min(5, "Minimum 5 minutes.")
    .max(10080, "Maximum 7 days (10080 minutes)."),
});

type ProfileValues = z.infer<typeof profileSchema>;
type SettingsValues = z.infer<typeof settingsSchema>;

export default function SettingsPage() {
  const queryClient = useQueryClient();
  const {
    activeTenantId,
    currentTenant,
    hasPermission,
    refreshProfile,
  } = useAuthTenant();

  const canEditProfile = hasPermission("tenant.update");
  const canEditSettings = hasPermission("tenant.settings.update");

  const [errorMsg, setErrorMsg] = React.useState<string | null>(null);
  const [successMsg, setSuccessMsg] = React.useState<string | null>(null);

  const settingsQuery = useQuery({
    queryKey: ["tenant", activeTenantId, "settings"],
    queryFn: () => tenantsApi.getSettings(),
    enabled: Boolean(activeTenantId) && hasPermission("tenant.settings.read"),
  });

  const sessionsQuery = useQuery({
    queryKey: ["userSessions"],
    queryFn: () => authApi.listSessions(),
  });

  const profileForm = useForm<ProfileValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: {
      name: currentTenant?.name ?? "",
      slug: currentTenant?.slug ?? "",
    },
  });

  const settingsForm = useForm<SettingsValues>({
    resolver: zodResolver(settingsSchema),
    defaultValues: {
      billingEmail: "",
      supportEmail: "",
      timezone: "UTC",
      locale: "en-US",
      enforceEmailDomain: false,
      allowedDomainsText: "",
      sessionTimeoutMinutes: 60,
    },
  });

  React.useEffect(() => {
    if (currentTenant) {
      profileForm.reset({
        name: currentTenant.name,
        slug: currentTenant.slug,
      });
    }
  }, [currentTenant, profileForm]);

  React.useEffect(() => {
    if (settingsQuery.data) {
      settingsForm.reset({
        billingEmail: settingsQuery.data.billingEmail,
        supportEmail: settingsQuery.data.supportEmail ?? "",
        timezone: settingsQuery.data.timezone,
        locale: settingsQuery.data.locale,
        enforceEmailDomain: settingsQuery.data.enforceEmailDomain,
        allowedDomainsText: settingsQuery.data.allowedEmailDomains.join(", "),
        sessionTimeoutMinutes: settingsQuery.data.sessionTimeoutMinutes,
      });
    }
  }, [settingsQuery.data, settingsForm]);

  const updateProfileMutation = useMutation({
    mutationFn: (values: ProfileValues) => tenantsApi.updateCurrent(values),
    onSuccess: async (updated) => {
      setErrorMsg(null);
      setSuccessMsg(`Organization identity updated to "${updated.name}".`);
      await refreshProfile(updated.id);
      void queryClient.invalidateQueries({ queryKey: ["tenant", activeTenantId] });
    },
    onError: (err) => setErrorMsg(extractErrorMessage(err)),
  });

  const updateSettingsMutation = useMutation({
    mutationFn: (values: SettingsValues) => {
      const domains = values.allowedDomainsText
        .split(",")
        .map((d) => d.trim().toLowerCase())
        .filter(Boolean);
      return tenantsApi.updateSettings({
        billingEmail: values.billingEmail,
        supportEmail: values.supportEmail ? values.supportEmail : null,
        timezone: values.timezone,
        locale: values.locale,
        enforceEmailDomain: values.enforceEmailDomain,
        allowedEmailDomains: domains,
        sessionTimeoutMinutes: values.sessionTimeoutMinutes,
      });
    },
    onSuccess: () => {
      setErrorMsg(null);
      setSuccessMsg("Organization security and workspace settings saved.");
      void queryClient.invalidateQueries({
        queryKey: ["tenant", activeTenantId, "settings"],
      });
    },
    onError: (err) => setErrorMsg(extractErrorMessage(err)),
  });

  const revokeSessionMutation = useMutation({
    mutationFn: (sessionId: string) => authApi.revokeSession(sessionId),
    onSuccess: () => {
      setErrorMsg(null);
      setSuccessMsg("Session revoked.");
      void queryClient.invalidateQueries({ queryKey: ["userSessions"] });
    },
    onError: (err) => setErrorMsg(extractErrorMessage(err)),
  });

  const enforceDomain = settingsForm.watch("enforceEmailDomain");

  return (
    <AuthGuard requireTenant requiredPermission="tenant.settings.read">
      <AppShell>
        <div className="space-y-6">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">
              Organization Settings & Security
            </h1>
            <p className="mt-1 text-sm text-muted-foreground">
              Configure tenant identity, security policy, email domain rules,
              and active user sessions.
            </p>
          </div>

          {errorMsg && (
            <Alert variant="destructive" title="Save Failed">
              {errorMsg}
            </Alert>
          )}
          {successMsg && (
            <Alert variant="success" title="Saved">
              {successMsg}
            </Alert>
          )}

          {/* Organization Profile */}
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Building2 className="h-4 w-4" />
                Organization Profile
              </CardTitle>
              <CardDescription>
                Display name and unique workspace slug identifier.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <form
                onSubmit={profileForm.handleSubmit((v) =>
                  updateProfileMutation.mutate(v)
                )}
                className="grid gap-4 sm:grid-cols-2"
              >
                <div className="space-y-1.5">
                  <Label htmlFor="tenant-name">Organization Name</Label>
                  <Input
                    id="tenant-name"
                    disabled={!canEditProfile}
                    {...profileForm.register("name")}
                  />
                  {profileForm.formState.errors.name && (
                    <p className="text-xs text-destructive">
                      {profileForm.formState.errors.name.message}
                    </p>
                  )}
                </div>

                <div className="space-y-1.5">
                  <Label htmlFor="tenant-slug">Workspace Slug</Label>
                  <Input
                    id="tenant-slug"
                    disabled={!canEditProfile}
                    className="font-mono text-sm"
                    {...profileForm.register("slug")}
                  />
                  {profileForm.formState.errors.slug && (
                    <p className="text-xs text-destructive">
                      {profileForm.formState.errors.slug.message}
                    </p>
                  )}
                </div>

                {canEditProfile && (
                  <div className="sm:col-span-2 flex justify-end">
                    <Button
                      type="submit"
                      loading={updateProfileMutation.isPending}
                    >
                      <Save className="h-4 w-4" />
                      Save Organization Identity
                    </Button>
                  </div>
                )}
              </form>
            </CardContent>
          </Card>

          {/* Tenant Security & Locale Settings */}
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <ShieldCheck className="h-4 w-4" />
                Workspace Policies & Communications
              </CardTitle>
              <CardDescription>
                {settingsQuery.data
                  ? `Last updated ${formatDateTime(settingsQuery.data.updatedAtUtc)}`
                  : "Loading workspace settings..."}
              </CardDescription>
            </CardHeader>
            <CardContent>
              <form
                onSubmit={settingsForm.handleSubmit((v) =>
                  updateSettingsMutation.mutate(v)
                )}
                className="space-y-5"
              >
                <div className="grid gap-4 sm:grid-cols-2">
                  <div className="space-y-1.5">
                    <Label htmlFor="billing-email">Billing Contact Email</Label>
                    <Input
                      id="billing-email"
                      type="email"
                      disabled={!canEditSettings}
                      {...settingsForm.register("billingEmail")}
                    />
                    {settingsForm.formState.errors.billingEmail && (
                      <p className="text-xs text-destructive">
                        {settingsForm.formState.errors.billingEmail.message}
                      </p>
                    )}
                  </div>

                  <div className="space-y-1.5">
                    <Label htmlFor="support-email">Support Email</Label>
                    <Input
                      id="support-email"
                      type="email"
                      disabled={!canEditSettings}
                      placeholder="support@company.com"
                      {...settingsForm.register("supportEmail")}
                    />
                    {settingsForm.formState.errors.supportEmail && (
                      <p className="text-xs text-destructive">
                        {settingsForm.formState.errors.supportEmail.message}
                      </p>
                    )}
                  </div>

                  <div className="space-y-1.5">
                    <Label htmlFor="timezone">Default Timezone</Label>
                    <Select
                      id="timezone"
                      disabled={!canEditSettings}
                      {...settingsForm.register("timezone")}
                    >
                      <option value="UTC">UTC</option>
                      <option value="America/New_York">America/New_York</option>
                      <option value="America/Los_Angeles">
                        America/Los_Angeles
                      </option>
                      <option value="Europe/London">Europe/London</option>
                      <option value="Europe/Berlin">Europe/Berlin</option>
                      <option value="Asia/Tokyo">Asia/Tokyo</option>
                      <option value="Asia/Dhaka">Asia/Dhaka</option>
                    </Select>
                  </div>

                  <div className="space-y-1.5">
                    <Label htmlFor="locale">Locale</Label>
                    <Select
                      id="locale"
                      disabled={!canEditSettings}
                      {...settingsForm.register("locale")}
                    >
                      <option value="en-US">en-US</option>
                      <option value="en-GB">en-GB</option>
                      <option value="de-DE">de-DE</option>
                      <option value="fr-FR">fr-FR</option>
                      <option value="ja-JP">ja-JP</option>
                    </Select>
                  </div>

                  <div className="space-y-1.5">
                    <Label htmlFor="session-timeout">
                      Session Timeout (minutes)
                    </Label>
                    <Input
                      id="session-timeout"
                      type="number"
                      disabled={!canEditSettings}
                      {...settingsForm.register("sessionTimeoutMinutes")}
                    />
                    {settingsForm.formState.errors.sessionTimeoutMinutes && (
                      <p className="text-xs text-destructive">
                        {
                          settingsForm.formState.errors.sessionTimeoutMinutes
                            .message
                        }
                      </p>
                    )}
                  </div>
                </div>

                <div className="space-y-3 rounded-lg border bg-muted/30 p-4">
                  <div className="flex items-center justify-between gap-4">
                    <div>
                      <Label htmlFor="enforce-domain">
                        Enforce Email Domain Restriction
                      </Label>
                      <p className="mt-1 text-xs text-muted-foreground">
                        Restrict new member invitations strictly to approved
                        corporate email domains.
                      </p>
                    </div>
                    <Switch
                      id="enforce-domain"
                      checked={enforceDomain}
                      disabled={!canEditSettings}
                      onCheckedChange={(checked) =>
                        settingsForm.setValue("enforceEmailDomain", checked)
                      }
                    />
                  </div>

                  <div className="space-y-1.5">
                    <Label htmlFor="allowed-domains">
                      Allowed Email Domains (comma-separated)
                    </Label>
                    <Input
                      id="allowed-domains"
                      disabled={!canEditSettings}
                      placeholder="acme.local, subsidiary.com"
                      {...settingsForm.register("allowedDomainsText")}
                    />
                  </div>
                </div>

                {canEditSettings && (
                  <div className="flex justify-end">
                    <Button
                      type="submit"
                      loading={updateSettingsMutation.isPending}
                    >
                      <Save className="h-4 w-4" />
                      Save Security Settings
                    </Button>
                  </div>
                )}
              </form>
            </CardContent>
          </Card>

          {/* Active User Sessions */}
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Laptop className="h-4 w-4" />
                Your Active Refresh Sessions
              </CardTitle>
              <CardDescription>
                Review signed-in devices across your account and revoke tokens
                immediately.
              </CardDescription>
            </CardHeader>
            <CardContent>
              {sessionsQuery.isLoading ? (
                <p className="py-4 text-sm text-muted-foreground">
                  Loading sessions...
                </p>
              ) : sessionsQuery.data?.length === 0 ? (
                <p className="py-4 text-sm text-muted-foreground">
                  No active refresh sessions.
                </p>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>IP Address</TableHead>
                      <TableHead>User Agent</TableHead>
                      <TableHead>Created</TableHead>
                      <TableHead>Last Seen</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead className="text-right">Action</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {sessionsQuery.data?.map((session) => (
                      <TableRow key={session.id}>
                        <TableCell className="font-mono text-xs">
                          {session.ipAddress}
                        </TableCell>
                        <TableCell className="max-w-xs truncate text-xs text-muted-foreground">
                          {session.userAgent}
                        </TableCell>
                        <TableCell className="text-xs text-muted-foreground">
                          {formatDateTime(session.createdAtUtc)}
                        </TableCell>
                        <TableCell className="text-xs text-muted-foreground">
                          {formatDateTime(session.lastSeenAtUtc)}
                        </TableCell>
                        <TableCell>
                          <Badge
                            variant={session.isActive ? "success" : "secondary"}
                          >
                            {session.isActive ? "Active" : "Inactive"}
                          </Badge>
                        </TableCell>
                        <TableCell className="text-right">
                          {session.isActive && (
                            <Button
                              variant="outline"
                              size="sm"
                              onClick={() =>
                                revokeSessionMutation.mutate(session.id)
                              }
                            >
                              <Trash2 className="h-3.5 w-3.5" />
                              Revoke
                            </Button>
                          )}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </div>
      </AppShell>
    </AuthGuard>
  );
}
