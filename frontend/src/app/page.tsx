"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { useAuthTenant } from "@/providers/AuthTenantProvider";
import { Loader2 } from "lucide-react";

export default function HomePage() {
  const router = useRouter();
  const { status, isPlatformAdmin, currentTenant, tenants } = useAuthTenant();

  React.useEffect(() => {
    if (status === "loading") return;
    if (status === "unauthenticated") {
      router.replace("/login");
      return;
    }
    if (isPlatformAdmin && tenants.length === 0) {
      router.replace("/platform");
      return;
    }
    if (!currentTenant || !currentTenant.onboardingCompleted) {
      router.replace("/onboarding");
      return;
    }
    router.replace("/dashboard");
  }, [status, isPlatformAdmin, currentTenant, tenants.length, router]);

  return (
    <div className="flex min-h-screen items-center justify-center bg-background">
      <div className="flex items-center gap-2 text-sm text-muted-foreground">
        <Loader2 className="h-4 w-4 animate-spin" />
        Loading workspace...
      </div>
    </div>
  );
}
