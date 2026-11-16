import { authStorage } from "@/lib/auth-storage";
import type { AuthTokenResponse, ProblemDetails } from "@/types/api";

export const API_BASE_URL = (
  process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080/api/v1"
).replace(/\/+$/, "");

export class ApiProblemError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails;
  readonly errorCode?: string;
  readonly correlationId?: string;
  readonly retryAfterSeconds: number | null;

  constructor(
    status: number,
    problem: ProblemDetails,
    retryAfterSeconds: number | null = null
  ) {
    const firstFieldError = problem.errors
      ? Object.values(problem.errors).flat()[0]
      : undefined;
    const message =
      firstFieldError ||
      problem.detail ||
      problem.title ||
      `Request failed with HTTP ${status}`;
    super(message);
    this.name = "ApiProblemError";
    this.status = status;
    this.problem = problem;
    this.errorCode = problem.errorCode;
    this.correlationId = problem.correlationId;
    this.retryAfterSeconds = retryAfterSeconds;
  }
}

export interface ApiRequestOptions extends Omit<RequestInit, "body"> {
  body?: unknown;
  query?: Record<string, string | number | boolean | null | undefined>;
  /** Set true on tenant-scoped endpoints to attach X-Tenant-Id */
  tenantScoped?: boolean;
  /** Override active tenant ID when needed (e.g., immediately during switch) */
  tenantIdOverride?: string;
  /** Omit Authorization header for anonymous auth calls */
  skipAuth?: boolean;
  /** Internal retry marker after single-flight refresh */
  _retried?: boolean;
}

let inFlightRefresh: Promise<AuthTokenResponse> | null = null;

async function parseProblemResponse(response: Response): Promise<ApiProblemError> {
  const retryAfterRaw = response.headers.get("Retry-After");
  const retryAfterSeconds = retryAfterRaw ? Number.parseInt(retryAfterRaw, 10) : null;
  const safeRetryAfter =
    retryAfterSeconds !== null && !Number.isNaN(retryAfterSeconds)
      ? retryAfterSeconds
      : null;

  try {
    const text = await response.text();
    if (!text) {
      return new ApiProblemError(
        response.status,
        { status: response.status, title: response.statusText || "Request failed" },
        safeRetryAfter
      );
    }
    const parsed = JSON.parse(text) as ProblemDetails;
    return new ApiProblemError(response.status, parsed, safeRetryAfter);
  } catch {
    return new ApiProblemError(
      response.status,
      { status: response.status, title: response.statusText || "Request failed" },
      safeRetryAfter
    );
  }
}

/**
 * Single-flight refresh lock: prevents concurrent 401 responses from triggering
 * parallel refresh token rotations that trigger backend family reuse revocation.
 */
export async function refreshSessionSingleFlight(): Promise<AuthTokenResponse> {
  if (inFlightRefresh) {
    return inFlightRefresh;
  }

  const tokens = authStorage.getTokens();
  if (!tokens?.refreshToken) {
    authStorage.setTokens(null);
    throw new ApiProblemError(401, {
      status: 401,
      title: "Session expired",
      detail: "No refresh token available. Please sign in again.",
      errorCode: "missing_refresh_token",
    });
  }

  inFlightRefresh = (async () => {
    const response = await fetch(`${API_BASE_URL}/auth/refresh`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Accept: "application/json",
      },
      body: JSON.stringify({ refreshToken: tokens.refreshToken }),
    });

    if (!response.ok) {
      authStorage.setTokens(null);
      authStorage.setActiveTenantId(null);
      throw await parseProblemResponse(response);
    }

    const rotated = (await response.json()) as AuthTokenResponse;
    authStorage.setTokens({
      accessToken: rotated.accessToken,
      refreshToken: rotated.refreshToken,
      accessTokenExpiresAtUtc: rotated.accessTokenExpiresAtUtc,
      refreshTokenExpiresAtUtc: rotated.refreshTokenExpiresAtUtc,
    });
    return rotated;
  })().finally(() => {
    inFlightRefresh = null;
  });

  return inFlightRefresh;
}

function buildUrl(
  path: string,
  query?: Record<string, string | number | boolean | null | undefined>
): string {
  const normalizedPath = path.startsWith("/") ? path : `/${path}`;
  const url = new URL(`${API_BASE_URL}${normalizedPath}`);
  if (query) {
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== "") {
        url.searchParams.set(key, String(value));
      }
    }
  }
  return url.toString();
}

export async function apiRequest<T>(
  path: string,
  options: ApiRequestOptions = {}
): Promise<T> {
  const {
    body,
    query,
    tenantScoped = false,
    tenantIdOverride,
    skipAuth = false,
    _retried = false,
    headers: customHeaders,
    ...restInit
  } = options;

  const headers = new Headers(customHeaders);
  if (!headers.has("Accept")) {
    headers.set("Accept", "application/json");
  }
  if (body !== undefined && !(body instanceof FormData) && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  if (!skipAuth) {
    const tokens = authStorage.getTokens();
    if (tokens?.accessToken) {
      headers.set("Authorization", `Bearer ${tokens.accessToken}`);
    }
  }

  if (tenantScoped) {
    const tenantId = tenantIdOverride ?? authStorage.getActiveTenantId();
    if (!tenantId) {
      throw new ApiProblemError(400, {
        status: 400,
        title: "No active organization selected",
        detail: "Please select or create an organization workspace first.",
        errorCode: "tenant_not_selected",
      });
    }
    headers.set("X-Tenant-Id", tenantId);
  }

  let response: Response;
  try {
    response = await fetch(buildUrl(path, query), {
      ...restInit,
      headers,
      body:
        body === undefined
          ? undefined
          : body instanceof FormData
            ? body
            : JSON.stringify(body),
    });
  } catch {
    throw new ApiProblemError(0, {
      status: 0,
      title: "Backend API Unreachable",
      detail: `Could not connect to the backend API (${API_BASE_URL}). Ensure PostgreSQL and the ASP.NET Core API are running.`,
      errorCode: "network_unreachable",
    });
  }

  if (response.status === 401 && !skipAuth && !_retried) {
    await refreshSessionSingleFlight();
    return apiRequest<T>(path, {
      ...options,
      _retried: true,
    });
  }

  if (!response.ok) {
    throw await parseProblemResponse(response);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const contentType = response.headers.get("content-type") ?? "";
  if (contentType.includes("application/json")) {
    return (await response.json()) as T;
  }

  const text = await response.text();
  return (text ? JSON.parse(text) : undefined) as T;
}
