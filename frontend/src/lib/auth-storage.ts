export interface StoredTokens {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAtUtc: string;
  refreshTokenExpiresAtUtc: string;
}

const TOKEN_STORAGE_KEY = "saas.platform.tokens.v1";
const TENANT_STORAGE_KEY = "saas.platform.activeTenantId.v1";

type TokenListener = (tokens: StoredTokens | null) => void;
const listeners = new Set<TokenListener>();

function isBrowser(): boolean {
  return typeof window !== "undefined";
}

export const authStorage = {
  getTokens(): StoredTokens | null {
    if (!isBrowser()) return null;
    try {
      const raw = window.localStorage.getItem(TOKEN_STORAGE_KEY);
      if (!raw) return null;
      const parsed = JSON.parse(raw) as Partial<StoredTokens>;
      if (!parsed.accessToken || !parsed.refreshToken) return null;
      return {
        accessToken: parsed.accessToken,
        refreshToken: parsed.refreshToken,
        accessTokenExpiresAtUtc: parsed.accessTokenExpiresAtUtc ?? "",
        refreshTokenExpiresAtUtc: parsed.refreshTokenExpiresAtUtc ?? "",
      };
    } catch {
      return null;
    }
  },

  setTokens(tokens: StoredTokens | null): void {
    if (!isBrowser()) return;
    if (tokens) {
      window.localStorage.setItem(TOKEN_STORAGE_KEY, JSON.stringify(tokens));
    } else {
      window.localStorage.removeItem(TOKEN_STORAGE_KEY);
    }
    listeners.forEach((listener) => listener(tokens));
  },

  getActiveTenantId(): string | null {
    if (!isBrowser()) return null;
    return window.localStorage.getItem(TENANT_STORAGE_KEY);
  },

  setActiveTenantId(tenantId: string | null): void {
    if (!isBrowser()) return;
    if (tenantId) {
      window.localStorage.setItem(TENANT_STORAGE_KEY, tenantId);
    } else {
      window.localStorage.removeItem(TENANT_STORAGE_KEY);
    }
  },

  subscribe(listener: TokenListener): () => void {
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  },
};
