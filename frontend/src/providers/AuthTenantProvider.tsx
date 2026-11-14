"use client";

import * as React from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { authStorage } from "@/lib/auth-storage";
import { authApi, tenantsApi } from "@/lib/saas-api";
import type {
  AuthTokenResponse,
  CurrentUserResponse,
  TenantDetailDto,
  UserTenantSummaryDto,
} from "@/types/api";

export type AuthStatus = "loading" | "authenticated" | "unauthenticated";

interface AuthTenantContextValue {
  status: AuthStatus;
  user: CurrentUserResponse | null;
  tenants: UserTenantSummaryDto[];
  activeTenantId: string | null;
  activeTenantSummary: UserTenantSummaryDto | null;
  currentTenant: TenantDetailDto | null;
  isPlatformAdmin: boolean;
  isSwitchingTenant: boolean;
  hasPermission: (permission: string) => boolean;
  refreshProfile: (preferredTenantId?: string | null) => Promise<void>;
  applyAuthResponse: (response: AuthTokenResponse) => Promise<string>;
  switchTenant: (tenantId: string) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthTenantContext = React.createContext<AuthTenantContextValue | null>(
  null
);

export function AuthTenantProvider({
  children,
}: {
  children: React.ReactNode;
}) {
  const queryClient = useQueryClient();
  const router = useRouter();

  const [status, setStatus] = React.useState<AuthStatus>("loading");
  const [user, setUser] = React.useState<CurrentUserResponse | null>(null);
  const [activeTenantId, setActiveTenantId] = React.useState<string | null>(
    null
  );
  const [currentTenant, setCurrentTenant] =
    React.useState<TenantDetailDto | null>(null);
  const [isSwitchingTenant, setIsSwitchingTenant] = React.useState(false);

  const loadTenantContext = React.useCallback(
    async (
      me: CurrentUserResponse,
      preferredTenantId?: string | null
    ): Promise<{ resolvedTenantId: string | null; detail: TenantDetailDto | null }> => {
      const storedId = preferredTenantId ?? authStorage.getActiveTenantId();
      const matched = me.tenants.find((t) => t.tenantId === storedId);
      const nextTenantId =
        matched?.tenantId ?? me.tenants[0]?.tenantId ?? null;

      authStorage.setActiveTenantId(nextTenantId);
      setActiveTenantId(nextTenantId);

      if (!nextTenantId) {
        setCurrentTenant(null);
        return { resolvedTenantId: null, detail: null };
      }

      try {
        const detail = await tenantsApi.getCurrent(nextTenantId);
        setCurrentTenant(detail);
        return { resolvedTenantId: nextTenantId, detail };
      } catch {
        setCurrentTenant(null);
        return { resolvedTenantId: nextTenantId, detail: null };
      }
    },
    []
  );

  const refreshProfile = React.useCallback(
    async (preferredTenantId?: string | null) => {
      const tokens = authStorage.getTokens();
      if (!tokens) {
        setUser(null);
        setActiveTenantId(null);
        setCurrentTenant(null);
        setStatus("unauthenticated");
        return;
      }

      try {
        const me = await authApi.me();
        setUser(me);
        await loadTenantContext(me, preferredTenantId);
        setStatus("authenticated");
      } catch {
        authStorage.setTokens(null);
        authStorage.setActiveTenantId(null);
        setUser(null);
        setActiveTenantId(null);
        setCurrentTenant(null);
        setStatus("unauthenticated");
      }
    },
    [loadTenantContext]
  );

  React.useEffect(() => {
    void refreshProfile();
  }, [refreshProfile]);

  React.useEffect(() => {
    return authStorage.subscribe((tokens) => {
      if (!tokens) {
        setUser(null);
        setActiveTenantId(null);
        setCurrentTenant(null);
        setStatus("unauthenticated");
      }
    });
  }, []);

  const applyAuthResponse = React.useCallback(
    async (response: AuthTokenResponse): Promise<string> => {
      authStorage.setTokens({
        accessToken: response.accessToken,
        refreshToken: response.refreshToken,
        accessTokenExpiresAtUtc: response.accessTokenExpiresAtUtc,
        refreshTokenExpiresAtUtc: response.refreshTokenExpiresAtUtc,
      });

      const me = await authApi.me();
      setUser(me);
      const { detail } = await loadTenantContext(me);
      setStatus("authenticated");
      queryClient.clear();

      if (me.platformRole === "PlatformAdministrator" && me.tenants.length === 0) {
        return "/platform";
      }
      if (!detail || !detail.onboardingCompleted) {
        return "/onboarding";
      }
      return "/dashboard";
    },
    [loadTenantContext, queryClient]
  );

  const switchTenant = React.useCallback(
    async (tenantId: string) => {
      if (tenantId === activeTenantId) return;
      setIsSwitchingTenant(true);
      try {
        queryClient.removeQueries({ queryKey: ["tenant"] });
        authStorage.setActiveTenantId(tenantId);
        setActiveTenantId(tenantId);
        const detail = await tenantsApi.getCurrent(tenantId);
        setCurrentTenant(detail);
        if (!detail.onboardingCompleted) {
          router.push("/onboarding");
        }
      } finally {
        setIsSwitchingTenant(false);
      }
    },
    [activeTenantId, queryClient, router]
  );

  const logout = React.useCallback(async () => {
    const tokens = authStorage.getTokens();
    if (tokens?.refreshToken) {
      try {
        await authApi.logout(tokens.refreshToken);
      } catch {
        // Clear local session state even if network or token revocation fails
      }
    }
    authStorage.setTokens(null);
    authStorage.setActiveTenantId(null);
    queryClient.clear();
    setUser(null);
    setActiveTenantId(null);
    setCurrentTenant(null);
    setStatus("unauthenticated");
    router.push("/login");
  }, [queryClient, router]);

  const tenants = user?.tenants ?? [];
  const activeTenantSummary =
    tenants.find((t) => t.tenantId === activeTenantId) ?? null;
  const isPlatformAdmin = user?.platformRole === "PlatformAdministrator";

  const hasPermission = React.useCallback(
    (permission: string) => {
      if (!currentTenant) return false;
      return currentTenant.currentUserPermissions.includes(permission);
    },
    [currentTenant]
  );

  return (
    <AuthTenantContext.Provider
      value={{
        status,
        user,
        tenants,
        activeTenantId,
        activeTenantSummary,
        currentTenant,
        isPlatformAdmin,
        isSwitchingTenant,
        hasPermission,
        refreshProfile,
        applyAuthResponse,
        switchTenant,
        logout,
      }}
    >
      {children}
    </AuthTenantContext.Provider>
  );
}

export function useAuthTenant(): AuthTenantContextValue {
  const ctx = React.useContext(AuthTenantContext);
  if (!ctx) {
    throw new Error("useAuthTenant must be used within AuthTenantProvider");
  }
  return ctx;
}
