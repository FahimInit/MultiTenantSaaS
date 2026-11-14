"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { Loader2, ShieldAlert } from "lucide-react";
import { useAuthTenant } from "@/providers/AuthTenantProvider";
import { AppShell } from "@/components/layout/AppShell";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";

export function AuthGuard({
  children,
  requireTenant = false,
  requiredPermission,
  requirePlatformAdmin = false,
}: {
  children: React.ReactNode;
  requireTenant?: boolean;
  requiredPermission?: string;
  requirePlatformAdmin?: boolean;
}) {
  const router = useRouter();
  const {
    status,
    activeTenantId,
    isPlatformAdmin,
    hasPermission,
    tenants,
  } = useAuthTenant();

  React.useEffect(() => {
    if (status === "unauthenticated") {
      router.replace("/login");
      return;
    }
    if (
      status === "authenticated" &&
      requireTenant &&
      !activeTenantId &&
      tenants.length === 0
    ) {
      router.replace("/onboarding");
    }
  }, [status, requireTenant, activeTenantId, tenants.length, router]);

  if (status === "loading" || status === "unauthenticated") {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background">
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          Verifying session...
        </div>
      </div>
    );
  }

  if (requirePlatformAdmin && !isPlatformAdmin) {
    return (
      <AppShell>
        <div className="mx-auto max-w-xl py-10">
          <Alert
            variant="destructive"
            title="Platform Administrator Access Required"
          >
            <div className="mt-2 space-y-3">
              <p>
                Your account does not hold the <code>PlatformAdministrator</code>{" "}
                role required to access global cross-tenant administration.
              </p>
              <Button
                variant="outline"
                size="sm"
                onClick={() => router.push("/dashboard")}
              >
                Return to Organization Dashboard
              </Button>
            </div>
          </Alert>
        </div>
      </AppShell>
    );
  }

  if (
    requireTenant &&
    requiredPermission &&
    activeTenantId &&
    !hasPermission(requiredPermission)
  ) {
    return (
      <AppShell>
        <div className="mx-auto max-w-xl py-10">
          <div className="rounded-lg border bg-card p-6 text-center shadow-sm">
            <ShieldAlert className="mx-auto mb-3 h-8 w-8 text-amber-500" />
            <h2 className="text-lg font-semibold">Insufficient Permissions</h2>
            <p className="mt-1 text-sm text-muted-foreground">
              Your current organization role does not include{" "}
              <code className="rounded bg-muted px-1.5 py-0.5 text-xs">
                {requiredPermission}
              </code>
              .
            </p>
            <Button
              variant="outline"
              size="sm"
              className="mt-4"
              onClick={() => router.push("/dashboard")}
            >
              Back to Dashboard
            </Button>
          </div>
        </div>
      </AppShell>
    );
  }

  return <>{children}</>;
}
