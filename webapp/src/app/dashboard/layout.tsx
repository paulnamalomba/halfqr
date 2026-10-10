import type { Metadata } from "next";
import type { ReactNode } from "react";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { accountGet, requireUser } from "@/lib/server/identity";
import type { BillingOverview } from "@/lib/types/account";
import "../console.css";

export const metadata: Metadata = {
  title: { default: "Console", template: "%s | HalfQR Console" },
  robots: { index: false, follow: false },
};

export const dynamic = "force-dynamic";

export default async function DashboardLayout({ children }: { children: ReactNode }) {
  const user = await requireUser();
  const billing = await accountGet<BillingOverview>("billing");

  return (
    <DashboardShell user={user} entitlement={billing?.entitlement ?? null}>
      {children}
    </DashboardShell>
  );
}
