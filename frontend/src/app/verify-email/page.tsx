"use client";

import * as React from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Building2, MailCheck } from "lucide-react";
import { authApi } from "@/lib/saas-api";
import { extractErrorMessage } from "@/lib/formatters";
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

const verifySchema = z.object({
  userId: z.string().trim().uuid("Enter a valid User ID UUID."),
  token: z.string().trim().min(1, "Verification token is required."),
});

type VerifyValues = z.infer<typeof verifySchema>;

function VerifyEmailInner() {
  const searchParams = useSearchParams();
  const initialUserId = searchParams.get("userId") ?? "";
  const initialToken = searchParams.get("token") ?? "";
  const initialEmail = searchParams.get("email") ?? "";

  const [error, setError] = React.useState<string | null>(null);
  const [verified, setVerified] = React.useState(false);
  const [resendEmail, setResendEmail] = React.useState(initialEmail);
  const [resendFeedback, setResendFeedback] = React.useState<string | null>(
    null
  );

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<VerifyValues>({
    resolver: zodResolver(verifySchema),
    defaultValues: {
      userId: initialUserId,
      token: initialToken,
    },
  });

  const onSubmit = async (values: VerifyValues) => {
    setError(null);
    try {
      await authApi.verifyEmail(values);
      setVerified(true);
    } catch (err) {
      setError(extractErrorMessage(err));
    }
  };

  const handleResend = async () => {
    if (!resendEmail.trim()) return;
    setError(null);
    setResendFeedback(null);
    try {
      const res = await authApi.resendVerification(resendEmail.trim());
      setResendFeedback(res.message);
    } catch (err) {
      setError(extractErrorMessage(err));
    }
  };

  return (
    <Card className="w-full max-w-md">
      <CardHeader>
        <CardTitle className="text-xl">Verify Email Address</CardTitle>
        <CardDescription>
          Submit your user ID and verification token to activate your account.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {error && (
          <Alert variant="destructive" title="Verification Failed">
            {error}
          </Alert>
        )}

        {resendFeedback && <Alert variant="success">{resendFeedback}</Alert>}

        {verified ? (
          <div className="space-y-4">
            <Alert variant="success" title="Email Verified">
              Your email has been confirmed. You can now sign in and create or
              join an organization workspace.
            </Alert>
            <Link
              href="/login"
              className="inline-flex h-9 w-full items-center justify-center rounded-md bg-primary px-4 text-sm font-medium text-primary-foreground hover:bg-primary/90"
            >
              Continue to Sign In
            </Link>
          </div>
        ) : (
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="userId">User ID</Label>
              <Input
                id="userId"
                placeholder="00000000-0000-0000-0000-000000000000"
                className="font-mono text-xs"
                {...register("userId")}
              />
              {errors.userId && (
                <p className="text-xs text-destructive">
                  {errors.userId.message}
                </p>
              )}
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="token">Verification Token</Label>
              <Input
                id="token"
                placeholder="Paste verification token"
                className="font-mono text-xs"
                {...register("token")}
              />
              {errors.token && (
                <p className="text-xs text-destructive">
                  {errors.token.message}
                </p>
              )}
            </div>

            <Button type="submit" className="w-full" loading={isSubmitting}>
              <MailCheck className="h-4 w-4" />
              Verify Email
            </Button>
          </form>
        )}

        {!verified && (
          <div className="space-y-2 border-t pt-4">
            <Label htmlFor="resend-email" className="text-xs text-muted-foreground">
              Need a fresh verification token?
            </Label>
            <div className="flex gap-2">
              <Input
                id="resend-email"
                type="email"
                value={resendEmail}
                onChange={(e) => setResendEmail(e.target.value)}
                placeholder="you@company.com"
              />
              <Button
                variant="outline"
                onClick={() => void handleResend()}
                disabled={!resendEmail.trim()}
              >
                Resend
              </Button>
            </div>
          </div>
        )}

        <div className="border-t pt-3 text-center text-sm">
          <Link
            href="/login"
            className="text-muted-foreground hover:text-foreground hover:underline"
          >
            Back to Sign In
          </Link>
        </div>
      </CardContent>
    </Card>
  );
}

export default function VerifyEmailPage() {
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

      <React.Suspense
        fallback={
          <Card className="w-full max-w-md p-6 text-center text-sm text-muted-foreground">
            Loading verification form...
          </Card>
        }
      >
        <VerifyEmailInner />
      </React.Suspense>
    </div>
  );
}
