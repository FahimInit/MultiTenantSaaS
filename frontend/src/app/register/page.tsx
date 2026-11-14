"use client";

import * as React from "react";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Building2, CheckCircle2 } from "lucide-react";
import { authApi } from "@/lib/saas-api";
import { extractErrorMessage } from "@/lib/formatters";
import type { RegisterUserResponse } from "@/types/api";
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

const registerSchema = z
  .object({
    fullName: z
      .string()
      .trim()
      .min(2, "Full name must be at least 2 characters.")
      .max(120, "Full name cannot exceed 120 characters."),
    email: z.string().trim().email("Enter a valid email address."),
    password: z
      .string()
      .min(8, "Password must be at least 8 characters.")
      .regex(/[A-Z]/, "Include at least one uppercase letter.")
      .regex(/[a-z]/, "Include at least one lowercase letter.")
      .regex(/[0-9]/, "Include at least one digit.")
      .regex(/[^A-Za-z0-9]/, "Include at least one special character."),
    confirmPassword: z.string(),
  })
  .refine((data) => data.password === data.confirmPassword, {
    path: ["confirmPassword"],
    message: "Passwords do not match.",
  });

type RegisterValues = z.infer<typeof registerSchema>;

export default function RegisterPage() {
  const [serverError, setServerError] = React.useState<string | null>(null);
  const [registered, setRegistered] =
    React.useState<RegisterUserResponse | null>(null);
  const [resentMessage, setResentMessage] = React.useState<string | null>(
    null
  );

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<RegisterValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: {
      fullName: "",
      email: "",
      password: "",
      confirmPassword: "",
    },
  });

  const onSubmit = async (values: RegisterValues) => {
    setServerError(null);
    try {
      const response = await authApi.register({
        email: values.email,
        password: values.password,
        fullName: values.fullName,
      });
      setRegistered(response);
    } catch (err) {
      setServerError(extractErrorMessage(err));
    }
  };

  const handleResend = async () => {
    if (!registered) return;
    setResentMessage(null);
    setServerError(null);
    try {
      const res = await authApi.resendVerification(registered.email);
      setResentMessage(res.message);
    } catch (err) {
      setServerError(extractErrorMessage(err));
    }
  };

  return (
    <div className="flex min-h-screen flex-col items-center justify-center bg-background px-4 py-10">
      <div className="mb-6 flex items-center gap-2.5">
        <div className="flex h-8 w-8 items-center justify-center rounded-md bg-primary text-primary-foreground">
          <Building2 className="h-4 w-4" />
        </div>
        <span className="text-base font-semibold tracking-tight">
          TenantCore
        </span>
      </div>

      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle className="text-xl">Create your account</CardTitle>
          <CardDescription>
            Register an individual identity, verify your email, then create or
            join an organization.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {serverError && (
            <Alert variant="destructive" title="Registration Error">
              {serverError}
            </Alert>
          )}

          {registered ? (
            <div className="space-y-4">
              <Alert variant="success" title="Account Created">
                <div className="mt-1 space-y-2">
                  <p>
                    Registered <strong>{registered.email}</strong>.
                  </p>
                  {registered.emailVerificationRequired ? (
                    <p className="text-xs">
                      Check your email delivery logs or inbox for your
                      verification token to activate sign-in.
                    </p>
                  ) : (
                    <p className="text-xs">
                      You may sign in immediately with your credentials.
                    </p>
                  )}
                  <p className="rounded bg-background/70 px-2 py-1 font-mono text-xs">
                    User ID: {registered.userId}
                  </p>
                </div>
              </Alert>

              {resentMessage && (
                <Alert variant="default">{resentMessage}</Alert>
              )}

              <div className="flex flex-col gap-2">
                <Link
                  href={`/verify-email?userId=${encodeURIComponent(registered.userId)}&email=${encodeURIComponent(registered.email)}`}
                  className="inline-flex h-9 items-center justify-center gap-2 rounded-md bg-primary px-4 text-sm font-medium text-primary-foreground hover:bg-primary/90"
                >
                  <CheckCircle2 className="h-4 w-4" />
                  Enter Verification Token
                </Link>
                <Button
                  variant="outline"
                  onClick={() => void handleResend()}
                >
                  Resend Verification Email
                </Button>
                <Link
                  href="/login"
                  className="text-center text-sm text-muted-foreground hover:text-foreground hover:underline"
                >
                  Proceed to Sign In
                </Link>
              </div>
            </div>
          ) : (
            <form
              onSubmit={handleSubmit(onSubmit)}
              className="space-y-4"
              noValidate
            >
              <div className="space-y-1.5">
                <Label htmlFor="fullName">Full Name</Label>
                <Input
                  id="fullName"
                  placeholder="Jordan Taylor"
                  {...register("fullName")}
                />
                {errors.fullName && (
                  <p className="text-xs text-destructive">
                    {errors.fullName.message}
                  </p>
                )}
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="email">Work Email</Label>
                <Input
                  id="email"
                  type="email"
                  placeholder="jordan@company.com"
                  {...register("email")}
                />
                {errors.email && (
                  <p className="text-xs text-destructive">
                    {errors.email.message}
                  </p>
                )}
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="password">Password</Label>
                <Input
                  id="password"
                  type="password"
                  {...register("password")}
                />
                {errors.password && (
                  <p className="text-xs text-destructive">
                    {errors.password.message}
                  </p>
                )}
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="confirmPassword">Confirm Password</Label>
                <Input
                  id="confirmPassword"
                  type="password"
                  {...register("confirmPassword")}
                />
                {errors.confirmPassword && (
                  <p className="text-xs text-destructive">
                    {errors.confirmPassword.message}
                  </p>
                )}
              </div>

              <Button type="submit" className="w-full" loading={isSubmitting}>
                Create Account
              </Button>
            </form>
          )}

          <div className="border-t pt-4 text-center text-sm text-muted-foreground">
            Already have an account?{" "}
            <Link
              href="/login"
              className="font-medium text-foreground hover:underline"
            >
              Sign in
            </Link>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
