import * as React from "react";
import { AlertCircle, CheckCircle2, Info, AlertTriangle } from "lucide-react";
import { cn } from "@/lib/utils";

export type AlertVariant = "default" | "destructive" | "success" | "warning";

const alertStyles: Record<AlertVariant, string> = {
  default: "border-border bg-muted/50 text-foreground",
  destructive:
    "border-red-500/30 bg-red-500/10 text-red-800 dark:text-red-300",
  success:
    "border-emerald-500/30 bg-emerald-500/10 text-emerald-800 dark:text-emerald-300",
  warning:
    "border-amber-500/30 bg-amber-500/10 text-amber-800 dark:text-amber-300",
};

const icons: Record<AlertVariant, React.ReactNode> = {
  default: <Info className="h-4 w-4 shrink-0" />,
  destructive: <AlertCircle className="h-4 w-4 shrink-0" />,
  success: <CheckCircle2 className="h-4 w-4 shrink-0" />,
  warning: <AlertTriangle className="h-4 w-4 shrink-0" />,
};

export interface AlertProps extends React.HTMLAttributes<HTMLDivElement> {
  variant?: AlertVariant;
  title?: string;
}

export function Alert({
  variant = "default",
  title,
  children,
  className,
  ...props
}: AlertProps) {
  return (
    <div
      role="alert"
      className={cn(
        "flex items-start gap-3 rounded-lg border px-4 py-3 text-sm",
        alertStyles[variant],
        className
      )}
      {...props}
    >
      <div className="mt-0.5">{icons[variant]}</div>
      <div className="min-w-0 flex-1 space-y-1">
        {title && <p className="font-medium leading-none">{title}</p>}
        <div className="text-sm opacity-95">{children}</div>
      </div>
    </div>
  );
}
