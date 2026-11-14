import type { Metadata } from "next";
import "./globals.css";
import { QueryProvider } from "@/providers/QueryProvider";
import { ThemeProvider } from "@/providers/ThemeProvider";
import { AuthTenantProvider } from "@/providers/AuthTenantProvider";

export const metadata: Metadata = {
  title: "TenantCore — Multi-Tenant SaaS Control Plane",
  description:
    "Organization workspace administration, RBAC, billing quotas, and audit observability.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body className="min-h-screen font-sans antialiased">
        <ThemeProvider>
          <QueryProvider>
            <AuthTenantProvider>{children}</AuthTenantProvider>
          </QueryProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
