import type { ProblemDetails } from "@/types/api";

export function formatCurrencyCents(cents: number, currency = "USD"): string {
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency,
    minimumFractionDigits: cents % 100 === 0 ? 0 : 2,
    maximumFractionDigits: 2,
  }).format(cents / 100);
}

export function formatNumber(value: number): string {
  return new Intl.NumberFormat("en-US").format(value);
}

export function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return "—";
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return "—";
  return new Intl.DateTimeFormat("en-US", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(date);
}

export function formatDateShort(iso: string | null | undefined): string {
  if (!iso) return "—";
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return "—";
  return new Intl.DateTimeFormat("en-US", {
    month: "short",
    day: "numeric",
    year: "numeric",
  }).format(date);
}

export function slugify(input: string): string {
  return input
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 63);
}

export function prettyPrintJson(jsonText: string | null | undefined): string {
  if (!jsonText) return "";
  try {
    return JSON.stringify(JSON.parse(jsonText), null, 2);
  } catch {
    return jsonText;
  }
}

export function extractErrorMessage(err: unknown): string {
  if (err && typeof err === "object" && "problem" in err) {
    const apiErr = err as { message: string; problem?: ProblemDetails; retryAfterSeconds?: number | null };
    if (apiErr.retryAfterSeconds && apiErr.retryAfterSeconds > 0) {
      return `${apiErr.message} Retry after ${apiErr.retryAfterSeconds}s.`;
    }
    return apiErr.message;
  }
  if (err instanceof Error) return err.message;
  return "An unexpected error occurred.";
}
