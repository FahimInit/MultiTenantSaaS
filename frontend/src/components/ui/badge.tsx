import * as React from "react";
import { cn } from "@/lib/utils";

export type BadgeVariant =
  | "default"
  | "secondary"
  | "outline"
  | "success"
  | "warning"
  | "danger"
  | "info";

const badgeVariants: Record<BadgeVariant, string> = {
  default: "bg-primary text-primary-foreground border-transparent",
  secondary: "bg-secondary text-secondary-foreground border-transparent",
  outline: "border-border text-foreground bg-transparent",
  success:
    "border-emerald-500/25 bg-emerald-500/10 text-emerald-700 dark:text-emerald-400",
  warning:
    "border-amber-500/25 bg-amber-500/10 text-amber-700 dark:text-amber-400",
  danger: "border-red-500/25 bg-red-500/10 text-red-700 dark:text-red-400",
  info: "border-sky-500/25 bg-sky-500/10 text-sky-700 dark:text-sky-400",
};

export interface BadgeProps extends React.HTMLAttributes<HTMLSpanElement> {
  variant?: BadgeVariant;
}

export function Badge({
  className,
  variant = "secondary",
  ...props
}: BadgeProps) {
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1 rounded-md border px-2 py-0.5 text-xs font-medium transition-colors",
        badgeVariants[variant],
        className
      )}
      {...props}
    />
  );
}

export function statusBadgeVariant(status: string | null | undefined): BadgeVariant {
  switch (status) {
    case "Active":
    case "Accepted":
      return "success";
    case "Trial":
    case "Trialing":
    case "Pending":
      return "info";
    case "PastDue":
    case "Suspended":
      return "warning";
    case "Canceled":
    case "Revoked":
    case "Expired":
    case "Deactivated":
    case "Removed":
      return "danger";
    default:
      return "secondary";
  }
}
