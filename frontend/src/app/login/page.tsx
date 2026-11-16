"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Building2, KeyRound, MailCheck, Moon, Sun } from "lucide-react";
import { authApi } from "@/lib/saas-api";
import { ApiProblemError } from "@/lib/api-client";
import { extractErrorMessage } from "@/lib/formatters";
import { useAuthTenant } from "@/providers/AuthTenantProvider";
import { useTheme } from "@/providers/ThemeProvider";
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

const loginSchema = z.object({
  email: z.string().trim().email("Enter a valid work email address."),
  password: z.string().min(1, "Password is required."),
});

const resetSchema = z.object({
  email: z.string().trim().email("Enter a valid email address."),
  token: z.string().trim().min(1, "Reset token is required."),
  newPassword: z
    .string()
    .min(8, "Password must be at least 8 characters long."),
});

type LoginFormValues = z.infer<typeof loginSchema>;
type ResetFormValues = z.infer<typeof resetSchema>;

const DEMO_ACCOUNTS = [
  { email: "alice@acme.local", label: "Acme Owner (Pro)" },
  { email: "bob@acme.local", label: "Acme Admin" },
  { email: "carol@acme.local", label: "Acme Member" },
  { email: "dave@globex.local", label: "Globex Owner (Free)" },
  { email: "consultant@demo.local", label: "Multi-Org User" },
  { email: "platform.admin@saas.local", label: "Platform Admin" },
];

export default function LoginPage() {
  const router = useRouter();
  const { status, isPlatformAdmin, currentTenant, tenants, applyAuthResponse } =
    useAuthTenant();
  const { theme, mounted, toggleTheme } = useTheme();
  const [serverError, setServerError] = React.useState<string | null>(null);
  const [infoMessage, setInfoMessage] = React.useState<string | null>(null);
  const [needsVerificationEmail, setNeedsVerificationEmail] =
    React.useState<string | null>(null);
  const [mode, setMode] = React.useState<"login" | "forgot" | "reset">("login");

  const showDemoHints = process.env.NEXT_PUBLIC_SHOW_DEMO_HINTS === "true";
  const demoPassword = process.env.NEXT_PUBLIC_DEMO_PASSWORD ?? "";

  const {
    register,
    handleSubmit,
    setValue,
    getValues,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: "", password: "" },
  });

  const resetForm = useForm<ResetFormValues>({
    resolver: zodResolver(resetSchema),
    defaultValues: { email: "", token: "", newPassword: "" },
  });

  React.useEffect(() => {
    if (status === "authenticated" && !isSubmitting) {
      if (isPlatformAdmin && tenants.length === 0) {
        router.replace("/platform");
      } else if (!currentTenant || !currentTenant.onboardingCompleted) {
        router.replace("/onboarding");
      } else {
        router.replace("/dashboard");
      }
    }
  }, [
    status,
    isSubmitting,
    isPlatformAdmin,
    tenants.length,
    currentTenant,
    router,
  ]);

  const onLogin = async (values: LoginFormValues) => {
    setServerError(null);
    setInfoMessage(null);
    setNeedsVerificationEmail(null);
    try {
      const response = await authApi.login(values);
      const nextRoute = await applyAuthResponse(response);
      router.push(nextRoute);
    } catch (err) {
      if (
        err instanceof ApiProblemError &&
        err.errorCode === "email_not_verified"
      ) {
        setNeedsVerificationEmail(values.email);
      }
      setServerError(extractErrorMessage(err));
    }
  };

  const handleResendVerification = async () => {
    if (!needsVerificationEmail) return;
    setServerError(null);
    try {
      const res = await authApi.resendVerification(needsVerificationEmail);
      setInfoMessage(res.message);
    } catch (err) {
      setServerError(extractErrorMessage(err));
    }
  };

  const handleForgotPassword = async (e: React.FormEvent) => {
    e.preventDefault();
    setServerError(null);
    setInfoMessage(null);
    const email = getValues("email").trim();
    if (!email) {
      setServerError("Enter your email address first.");
      return;
    }
    try {
      const res = await authApi.forgotPassword(email);
      resetForm.setValue("email", email);
      setInfoMessage(res.message);
      setMode("reset");
    } catch (err) {
      setServerError(extractErrorMessage(err));
    }
  };

  const onResetPassword = async (values: ResetFormValues) => {
    setServerError(null);
    setInfoMessage(null);
    try {
      await authApi.resetPassword(values);
      setValue("email", values.email);
      setInfoMessage("Password updated. You can now sign in.");
      setMode("login");
    } catch (err) {
      setServerError(extractErrorMessage(err));
    }
  };

  return (
    <div className="flex min-h-screen flex-col justify-between bg-background px-4 py-8">
      <div className="mx-auto flex w-full max-w-md items-center justify-between">
        <div className="flex items-center gap-2.5">
          <div className="flex h-8 w-8 items-center justify-center rounded-md bg-primary text-primary-foreground">
            <Building2 className="h-4 w-4" />
          </div>
          <span className="text-sm font-semibold tracking-tight">
            TenantCore
          </span>
        </div>
        {mounted && (
          <Button
            variant="ghost"
            size="icon"
            onClick={toggleTheme}
            aria-label="Toggle color theme"
          >
            {theme === "dark" ? (
              <Sun className="h-4 w-4" />
            ) : (
              <Moon className="h-4 w-4" />
            )}
          </Button>
        )}
      </div>

      <div className="mx-auto my-6 w-full max-w-md space-y-5">
        <Card>
          <CardHeader>
            <CardTitle className="text-xl">
              {mode === "login"
                ? "Sign in to your workspace"
                : mode === "forgot"
                  ? "Request password reset"
                  : "Set a new password"}
            </CardTitle>
            <CardDescription>
              {mode === "login"
                ? "Authenticate with your organization credentials."
                : mode === "forgot"
                  ? "We will dispatch a short-lived reset token to your email."
                  : "Enter the reset token from your inbox or dev email log."}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {serverError && (
              <Alert variant="destructive" title="Authentication Error">
                <div className="space-y-2">
                  <p>{serverError}</p>
                  {needsVerificationEmail && (
                    <div className="flex flex-wrap gap-2 pt-1">
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => void handleResendVerification()}
                      >
                        <MailCheck className="h-3.5 w-3.5" />
                        Resend Verification Email
                      </Button>
                      <Link
                        href={`/verify-email?email=${encodeURIComponent(needsVerificationEmail)}`}
                        className="inline-flex h-8 items-center rounded-md border px-3 text-xs font-medium hover:bg-accent"
                      >
                        Verify Token
                      </Link>
                    </div>
                  )}
                </div>
              </Alert>
            )}

            {infoMessage && (
              <Alert variant="success" title="Update">
                {infoMessage}
              </Alert>
            )}

            {mode === "login" && (
              <form
                onSubmit={handleSubmit(onLogin)}
                className="space-y-4"
                noValidate
              >
                <div className="space-y-1.5">
                  <Label htmlFor="email">Work Email</Label>
                  <Input
                    id="email"
                    type="email"
                    autoComplete="email"
                    placeholder="alice@acme.local"
                    {...register("email")}
                  />
                  {errors.email && (
                    <p className="text-xs text-destructive">
                      {errors.email.message}
                    </p>
                  )}
                </div>

                <div className="space-y-1.5">
                  <div className="flex items-center justify-between">
                    <Label htmlFor="password">Password</Label>
                    <button
                      type="button"
                      onClick={() => {
                        setServerError(null);
                        setInfoMessage(null);
                        setMode("forgot");
                      }}
                      className="text-xs text-muted-foreground hover:text-foreground hover:underline"
                    >
                      Forgot password?
                    </button>
                  </div>
                  <Input
                    id="password"
                    type="password"
                    autoComplete="current-password"
                    placeholder="••••••••••••"
                    {...register("password")}
                  />
                  {errors.password && (
                    <p className="text-xs text-destructive">
                      {errors.password.message}
                    </p>
                  )}
                </div>

                <Button
                  type="submit"
                  className="w-full"
                  loading={isSubmitting}
                >
                  Sign in
                </Button>
              </form>
            )}

            {mode === "forgot" && (
              <form onSubmit={handleForgotPassword} className="space-y-4">
                <div className="space-y-1.5">
                  <Label htmlFor="forgot-email">Account Email</Label>
                  <Input
                    id="forgot-email"
                    type="email"
                    placeholder="alice@acme.local"
                    {...register("email")}
                  />
                </div>
                <div className="flex gap-2">
                  <Button type="submit" className="flex-1">
                    Send Reset Token
                  </Button>
                  <Button
                    variant="outline"
                    onClick={() => setMode("login")}
                  >
                    Back
                  </Button>
                </div>
              </form>
            )}

            {mode === "reset" && (
              <form
                onSubmit={resetForm.handleSubmit(onResetPassword)}
                className="space-y-4"
              >
                <div className="space-y-1.5">
                  <Label htmlFor="reset-email">Email</Label>
                  <Input
                    id="reset-email"
                    type="email"
                    {...resetForm.register("email")}
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="reset-token">Reset Token</Label>
                  <Input
                    id="reset-token"
                    placeholder="Paste token"
                    {...resetForm.register("token")}
                  />
                  {resetForm.formState.errors.token && (
                    <p className="text-xs text-destructive">
                      {resetForm.formState.errors.token.message}
                    </p>
                  )}
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="new-password">New Password</Label>
                  <Input
                    id="new-password"
                    type="password"
                    {...resetForm.register("newPassword")}
                  />
                  {resetForm.formState.errors.newPassword && (
                    <p className="text-xs text-destructive">
                      {resetForm.formState.errors.newPassword.message}
                    </p>
                  )}
                </div>
                <div className="flex gap-2">
                  <Button
                    type="submit"
                    className="flex-1"
                    loading={resetForm.formState.isSubmitting}
                  >
                    <KeyRound className="h-4 w-4" />
                    Reset Password
                  </Button>
                  <Button
                    variant="outline"
                    onClick={() => setMode("login")}
                  >
                    Cancel
                  </Button>
                </div>
              </form>
            )}

            <div className="border-t pt-4 text-center text-sm text-muted-foreground">
              Need an account?{" "}
              <Link
                href="/register"
                className="font-medium text-foreground hover:underline"
              >
                Create an account
              </Link>
              <span className="mx-2">·</span>
              <Link
                href="/verify-email"
                className="font-medium text-foreground hover:underline"
              >
                Verify email
              </Link>
            </div>
          </CardContent>
        </Card>

        {showDemoHints && mode === "login" && (
          <Card className="bg-muted/40">
            <CardHeader className="pb-2">
              <CardTitle className="text-sm">Seeded Development Personas</CardTitle>
              <CardDescription className="text-xs">
                {demoPassword
                  ? "Click an account to pre-fill email and local development password."
                  : "Click an account to pre-fill email, then enter your local SeedDemoData:DefaultPassword."}
              </CardDescription>
            </CardHeader>
            <CardContent className="grid grid-cols-1 gap-1.5 sm:grid-cols-2">
              {DEMO_ACCOUNTS.map((demo) => (
                <button
                  key={demo.email}
                  type="button"
                  onClick={() => {
                    setValue("email", demo.email, { shouldValidate: true });
                    if (demoPassword) {
                      setValue("password", demoPassword, {
                        shouldValidate: true,
                      });
                    }
                  }}
                  className="rounded-md border bg-background px-2.5 py-1.5 text-left transition-colors hover:bg-accent"
                >
                  <p className="text-xs font-medium">{demo.label}</p>
                  <p className="truncate font-mono text-[11px] text-muted-foreground">
                    {demo.email}
                  </p>
                </button>
              ))}
            </CardContent>
          </Card>
        )}
      </div>

      <p className="text-center text-xs text-muted-foreground">
        Multi-Tenant SaaS Management Platform · ASP.NET Core & Next.js
      </p>
    </div>
  );
}
