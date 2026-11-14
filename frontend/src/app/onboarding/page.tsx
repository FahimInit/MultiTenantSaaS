"use client";

import * as React from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import {
  ArrowRight,
  Building2,
  CheckCircle2,
  MailPlus,
  Sparkles,
} from "lucide-react";
import { invitationsApi, tenantsApi } from "@/lib/saas-api";
import { extractErrorMessage, slugify } from "@/lib/formatters";
import { useAuthTenant } from "@/providers/AuthTenantProvider";
import { AuthGuard } from "@/components/shared/WorkspaceGuard";
import { AppShell } from "@/components/layout/AppShell";
import { Button } from "@/components/ui/button";
import { Input, Label } from "@/components/ui/form-controls";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Alert } from "@/components/ui/alert";
import { Badge, statusBadgeVariant } from "@/components/ui/badge";

const createTenantSchema = z.object({
  name: z
    .string()
    .trim()
    .min(2, "Organization name must be at least 2 characters.")
    .max(120),
  slug: z
    .string()
    .trim()
    .min(3, "Slug must be at least 3 characters.")
    .max(63)
    .regex(
      /^[a-z0-9]+(?:-[a-z0-9]+)*$/,
      "Use lowercase letters, numbers, and single hyphens."
    ),
});

const acceptInviteSchema = z.object({
  token: z.string().trim().min(6, "Enter the full invitation token."),
});

type CreateTenantValues = z.infer<typeof createTenantSchema>;
type AcceptInviteValues = z.infer<typeof acceptInviteSchema>;

function OnboardingContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const inviteTokenParam = searchParams.get("inviteToken") ?? "";
  const {
    currentTenant,
    hasPermission,
    refreshProfile,
  } = useAuthTenant();

  const [error, setError] = React.useState<string | null>(null);
  const [success, setSuccess] = React.useState<string | null>(null);
  const [slugEdited, setSlugEdited] = React.useState(false);
  const [completingOnboarding, setCompletingOnboarding] = React.useState(false);

  const createForm = useForm<CreateTenantValues>({
    resolver: zodResolver(createTenantSchema),
    defaultValues: { name: "", slug: "" },
  });

  const inviteForm = useForm<AcceptInviteValues>({
    resolver: zodResolver(acceptInviteSchema),
    defaultValues: { token: inviteTokenParam },
  });

  const watchedName = createForm.watch("name");
  React.useEffect(() => {
    if (!slugEdited) {
      createForm.setValue("slug", slugify(watchedName), {
        shouldValidate: watchedName.length >= 2,
      });
    }
  }, [watchedName, slugEdited, createForm]);

  const onCreateTenant = async (values: CreateTenantValues) => {
    setError(null);
    setSuccess(null);
    try {
      const created = await tenantsApi.create(values);
      await refreshProfile(created.id);
      createForm.reset({ name: "", slug: "" });
      setSlugEdited(false);
      setSuccess(
        `Workspace "${created.name}" created with a trial subscription. Complete onboarding below when ready.`
      );
    } catch (err) {
      setError(extractErrorMessage(err));
    }
  };

  const handleCompleteOnboarding = async () => {
    setError(null);
    setSuccess(null);
    setCompletingOnboarding(true);
    try {
      await tenantsApi.completeOnboarding();
      await refreshProfile(currentTenant?.id ?? null);
      router.push("/dashboard");
    } catch (err) {
      setError(extractErrorMessage(err));
    } finally {
      setCompletingOnboarding(false);
    }
  };

  const onAcceptInvite = async (values: AcceptInviteValues) => {
    setError(null);
    setSuccess(null);
    try {
      const joined = await invitationsApi.accept(values.token);
      await refreshProfile(joined.tenantId);
      setSuccess(
        `Joined ${joined.tenantName} as ${joined.roleName}. Redirecting to dashboard...`
      );
      router.push("/dashboard");
    } catch (err) {
      setError(extractErrorMessage(err));
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">
          Workspace Onboarding & Invitations
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Launch a new B2B organization, finalize onboarding, or accept a team
          invitation.
        </p>
      </div>

      {error && (
        <Alert variant="destructive" title="Action Required">
          {error}
        </Alert>
      )}
      {success && (
        <Alert variant="success" title="Success">
          {success}
        </Alert>
      )}

      {currentTenant && (
        <Card className="border-primary/25">
          <CardHeader>
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div>
                <CardTitle className="flex items-center gap-2">
                  <Sparkles className="h-4 w-4 text-primary" />
                  Active Workspace: {currentTenant.name}
                </CardTitle>
                <CardDescription>
                  Slug <code>/{currentTenant.slug}</code> · Your role:{" "}
                  <code>{currentTenant.currentUserRoleCode}</code>
                </CardDescription>
              </div>
              <div className="flex items-center gap-2">
                <Badge variant={statusBadgeVariant(currentTenant.status)}>
                  {currentTenant.status}
                </Badge>
                <Badge
                  variant={
                    currentTenant.onboardingCompleted ? "success" : "warning"
                  }
                >
                  {currentTenant.onboardingCompleted
                    ? "Onboarding Complete"
                    : "Onboarding Pending"}
                </Badge>
              </div>
            </div>
          </CardHeader>
          <CardContent className="flex flex-wrap items-center justify-between gap-4">
            <div className="space-y-1 text-sm text-muted-foreground">
              {currentTenant.onboardingCompleted ? (
                <p>
                  This organization has completed setup and is ready for daily
                  operations.
                </p>
              ) : (
                <p>
                  Review your team settings and mark workspace onboarding as
                  complete to activate full operations.
                </p>
              )}
            </div>
            <div className="flex flex-wrap gap-2">
              {!currentTenant.onboardingCompleted &&
                hasPermission("tenant.update") && (
                  <Button
                    loading={completingOnboarding}
                    onClick={() => void handleCompleteOnboarding()}
                  >
                    <CheckCircle2 className="h-4 w-4" />
                    Complete Onboarding
                  </Button>
                )}
              <Button
                variant="outline"
                onClick={() => router.push("/dashboard")}
              >
                Go to Dashboard
                <ArrowRight className="h-4 w-4" />
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Building2 className="h-4 w-4" />
              Create New Organization
            </CardTitle>
            <CardDescription>
              Provisions an isolated tenant workspace, default RBAC roles, and
              an initial Free plan subscription.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <form
              onSubmit={createForm.handleSubmit(onCreateTenant)}
              className="space-y-4"
            >
              <div className="space-y-1.5">
                <Label htmlFor="org-name">Organization Name</Label>
                <Input
                  id="org-name"
                  placeholder="Northstar Logistics"
                  {...createForm.register("name")}
                />
                {createForm.formState.errors.name && (
                  <p className="text-xs text-destructive">
                    {createForm.formState.errors.name.message}
                  </p>
                )}
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="org-slug">Workspace Slug</Label>
                <Input
                  id="org-slug"
                  placeholder="northstar-logistics"
                  className="font-mono text-sm"
                  {...createForm.register("slug", {
                    onChange: () => setSlugEdited(true),
                  })}
                />
                {createForm.formState.errors.slug && (
                  <p className="text-xs text-destructive">
                    {createForm.formState.errors.slug.message}
                  </p>
                )}
              </div>

              <Button
                type="submit"
                className="w-full"
                loading={createForm.formState.isSubmitting}
              >
                Create Organization Workspace
              </Button>
            </form>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <MailPlus className="h-4 w-4" />
              Accept Organization Invitation
            </CardTitle>
            <CardDescription>
              Join an existing tenant workspace using an invitation token sent
              to your verified email address.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <form
              onSubmit={inviteForm.handleSubmit(onAcceptInvite)}
              className="space-y-4"
            >
              <div className="space-y-1.5">
                <Label htmlFor="invite-token">Invitation Token</Label>
                <Input
                  id="invite-token"
                  placeholder="Paste opaque invitation token"
                  className="font-mono text-xs"
                  {...inviteForm.register("token")}
                />
                {inviteForm.formState.errors.token && (
                  <p className="text-xs text-destructive">
                    {inviteForm.formState.errors.token.message}
                  </p>
                )}
              </div>

              <Button
                type="submit"
                variant="secondary"
                className="w-full"
                loading={inviteForm.formState.isSubmitting}
              >
                Accept Invitation & Switch Workspace
              </Button>
            </form>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

export default function OnboardingPage() {
  return (
    <AuthGuard>
      <AppShell>
        <React.Suspense
          fallback={
            <p className="text-sm text-muted-foreground">
              Loading onboarding...
            </p>
          }
        >
          <OnboardingContent />
        </React.Suspense>
      </AppShell>
    </AuthGuard>
  );
}
