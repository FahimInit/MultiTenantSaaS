"use client";

import * as React from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import {
  Activity,
  Building2,
  CreditCard,
  LayoutDashboard,
  LogOut,
  Menu,
  Moon,
  Plus,
  Settings,
  Shield,
  ShieldCheck,
  Sun,
  Users,
  X,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { useAuthTenant } from "@/providers/AuthTenantProvider";
import { useTheme } from "@/providers/ThemeProvider";
import { Badge, statusBadgeVariant } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Select } from "@/components/ui/form-controls";
import { Alert } from "@/components/ui/alert";

interface NavItem {
  href: string;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
  permission?: string;
}

const tenantNavItems: NavItem[] = [
  {
    href: "/dashboard",
    label: "Overview",
    icon: LayoutDashboard,
    permission: "dashboard.read",
  },
  {
    href: "/members",
    label: "Members",
    icon: Users,
    permission: "members.read",
  },
  {
    href: "/roles",
    label: "Roles & RBAC",
    icon: Shield,
    permission: "roles.read",
  },
  {
    href: "/subscription",
    label: "Subscription",
    icon: CreditCard,
    permission: "billing.read",
  },
  {
    href: "/settings",
    label: "Settings",
    icon: Settings,
    permission: "tenant.settings.read",
  },
  {
    href: "/audit-logs",
    label: "Audit Logs",
    icon: Activity,
    permission: "audit.read",
  },
];

export function AppShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const {
    user,
    tenants,
    activeTenantId,
    activeTenantSummary,
    currentTenant,
    isPlatformAdmin,
    isSwitchingTenant,
    hasPermission,
    switchTenant,
    logout,
  } = useAuthTenant();
  const { theme, mounted, toggleTheme } = useTheme();
  const [mobileOpen, setMobileOpen] = React.useState(false);

  const visibleTenantNav = React.useMemo(() => {
    if (!currentTenant) return [];
    return tenantNavItems.filter(
      (item) => !item.permission || hasPermission(item.permission)
    );
  }, [currentTenant, hasPermission]);

  const handleWorkspaceSelect = async (
    event: React.ChangeEvent<HTMLSelectElement>
  ) => {
    const val = event.target.value;
    if (val === "__new__") {
      router.push("/onboarding");
      return;
    }
    await switchTenant(val);
  };

  const sidebarContent = (
    <div className="flex h-full flex-col justify-between p-4">
      <div className="space-y-6">
        <div className="flex items-center justify-between px-1">
          <Link href="/dashboard" className="flex items-center gap-2.5">
            <div className="flex h-8 w-8 items-center justify-center rounded-md bg-primary text-primary-foreground">
              <Building2 className="h-4 w-4" />
            </div>
            <div>
              <p className="text-sm font-semibold leading-none tracking-tight">
                TenantCore
              </p>
              <p className="mt-0.5 text-[11px] text-muted-foreground">
                SaaS Control Plane
              </p>
            </div>
          </Link>
        </div>

        {/* Workspace Switcher */}
        <div className="space-y-1.5 rounded-lg border bg-background p-2.5">
          <div className="flex items-center justify-between">
            <span className="text-[11px] font-medium uppercase tracking-wider text-muted-foreground">
              Workspace
            </span>
            {activeTenantSummary && (
              <Badge variant={statusBadgeVariant(activeTenantSummary.status)}>
                {activeTenantSummary.status}
              </Badge>
            )}
          </div>
          {tenants.length > 0 ? (
            <Select
              aria-label="Active workspace"
              value={activeTenantId ?? ""}
              disabled={isSwitchingTenant}
              onChange={(e) => void handleWorkspaceSelect(e)}
              className="h-8 text-xs font-medium"
            >
              {tenants.map((tenant) => (
                <option key={tenant.tenantId} value={tenant.tenantId}>
                  {tenant.name} ({tenant.roleCode})
                </option>
              ))}
              <option value="__new__">+ New Organization...</option>
            </Select>
          ) : (
            <Button
              variant="outline"
              size="sm"
              className="w-full justify-start text-xs"
              onClick={() => router.push("/onboarding")}
            >
              <Plus className="h-3.5 w-3.5" />
              Create Organization
            </Button>
          )}
          {currentTenant && (
            <p className="truncate px-0.5 text-[11px] text-muted-foreground">
              Role:{" "}
              <span className="font-medium text-foreground">
                {currentTenant.currentUserRoleCode}
              </span>
            </p>
          )}
        </div>

        {/* Navigation Links */}
        <nav className="space-y-1" aria-label="Primary navigation">
          {visibleTenantNav.length > 0 && (
            <div className="space-y-1">
              <p className="px-2 pb-1 text-[11px] font-medium uppercase tracking-wider text-muted-foreground">
                Organization
              </p>
              {visibleTenantNav.map((item) => {
                const Icon = item.icon;
                const active = pathname === item.href;
                return (
                  <Link
                    key={item.href}
                    href={item.href}
                    onClick={() => setMobileOpen(false)}
                    className={cn(
                      "flex items-center gap-2.5 rounded-md px-2.5 py-2 text-sm font-medium transition-colors",
                      active
                        ? "bg-primary text-primary-foreground"
                        : "text-muted-foreground hover:bg-accent hover:text-accent-foreground"
                    )}
                  >
                    <Icon className="h-4 w-4 shrink-0" />
                    {item.label}
                  </Link>
                );
              })}
            </div>
          )}

          <div className="pt-3">
            <p className="px-2 pb-1 text-[11px] font-medium uppercase tracking-wider text-muted-foreground">
              Workspace Setup
            </p>
            <Link
              href="/onboarding"
              onClick={() => setMobileOpen(false)}
              className={cn(
                "flex items-center gap-2.5 rounded-md px-2.5 py-2 text-sm font-medium transition-colors",
                pathname === "/onboarding"
                  ? "bg-primary text-primary-foreground"
                  : "text-muted-foreground hover:bg-accent hover:text-accent-foreground"
              )}
            >
              <Plus className="h-4 w-4 shrink-0" />
              Onboarding & Invites
            </Link>
          </div>

          {isPlatformAdmin && (
            <div className="pt-3">
              <p className="px-2 pb-1 text-[11px] font-medium uppercase tracking-wider text-muted-foreground">
                Global Control
              </p>
              <Link
                href="/platform"
                onClick={() => setMobileOpen(false)}
                className={cn(
                  "flex items-center gap-2.5 rounded-md px-2.5 py-2 text-sm font-medium transition-colors",
                  pathname === "/platform"
                    ? "bg-primary text-primary-foreground"
                    : "text-muted-foreground hover:bg-accent hover:text-accent-foreground"
                )}
              >
                <ShieldCheck className="h-4 w-4 shrink-0" />
                Platform Admin
              </Link>
            </div>
          )}
        </nav>
      </div>

      {/* User & Sign Out */}
      <div className="space-y-3 border-t pt-4">
        <div className="flex items-center justify-between gap-2 px-1">
          <div className="min-w-0">
            <p className="truncate text-sm font-medium">
              {user?.fullName ?? "User"}
            </p>
            <p className="truncate text-xs text-muted-foreground">
              {user?.email ?? ""}
            </p>
          </div>
          {mounted && (
            <Button
              variant="ghost"
              size="icon"
              onClick={toggleTheme}
              aria-label="Toggle color theme"
              title="Toggle light/dark theme"
            >
              {theme === "dark" ? (
                <Sun className="h-4 w-4" />
              ) : (
                <Moon className="h-4 w-4" />
              )}
            </Button>
          )}
        </div>
        <Button
          variant="outline"
          size="sm"
          className="w-full justify-start"
          onClick={() => void logout()}
        >
          <LogOut className="h-4 w-4" />
          Sign out
        </Button>
      </div>
    </div>
  );

  return (
    <div className="flex min-h-screen bg-background">
      {/* Desktop Sidebar */}
      <aside className="hidden w-64 shrink-0 border-r bg-card lg:block">
        <div className="sticky top-0 h-screen overflow-y-auto">
          {sidebarContent}
        </div>
      </aside>

      {/* Mobile Drawer */}
      {mobileOpen && (
        <div className="fixed inset-0 z-50 flex lg:hidden">
          <div
            className="fixed inset-0 bg-black/50"
            onClick={() => setMobileOpen(false)}
          />
          <aside className="relative z-10 w-64 border-r bg-card shadow-xl">
            <Button
              variant="ghost"
              size="icon"
              aria-label="Close navigation"
              onClick={() => setMobileOpen(false)}
              className="absolute right-2 top-3 h-8 w-8"
            >
              <X className="h-4 w-4" />
            </Button>
            {sidebarContent}
          </aside>
        </div>
      )}

      {/* Main Column */}
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-30 flex h-14 items-center justify-between border-b bg-card/85 px-4 backdrop-blur-sm lg:px-8">
          <div className="flex items-center gap-3">
            <Button
              variant="ghost"
              size="icon"
              className="lg:hidden"
              onClick={() => setMobileOpen(true)}
              aria-label="Open navigation"
            >
              <Menu className="h-5 w-5" />
            </Button>
            <div className="flex items-center gap-2 text-sm">
              <span className="font-medium text-foreground">
                {currentTenant?.name ??
                  (pathname === "/platform"
                    ? "Platform Administration"
                    : "Workspace Setup")}
              </span>
              {currentTenant && (
                <span className="hidden font-mono text-xs text-muted-foreground sm:inline">
                  /{currentTenant.slug}
                </span>
              )}
            </div>
          </div>
          <div className="flex items-center gap-2">
            {isPlatformAdmin && pathname !== "/platform" && (
              <Link
                href="/platform"
                className="hidden rounded-md border px-2.5 py-1 text-xs font-medium text-muted-foreground transition-colors hover:bg-accent hover:text-foreground sm:inline-flex sm:items-center sm:gap-1.5"
              >
                <ShieldCheck className="h-3.5 w-3.5" />
                Platform Console
              </Link>
            )}
          </div>
        </header>

        <main className="mx-auto w-full max-w-6xl flex-1 p-4 lg:p-8">
          {currentTenant?.status === "Suspended" && (
            <Alert
              variant="destructive"
              title="Organization Suspended"
              className="mb-6"
            >
              This workspace is currently suspended by a Platform Administrator.
              Write actions are restricted until reactivated.
            </Alert>
          )}
          {children}
        </main>
      </div>
    </div>
  );
}
