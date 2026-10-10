import type { Metadata } from "next";
import { Notice, PageHeader } from "@/components/console/ui";
import { BillingPanel } from "@/components/dashboard/billing-panel";
import { accountGet } from "@/lib/server/identity";
import type { BillingOverview } from "@/lib/types/account";

export const metadata: Metadata = { title: "Plan & billing" };

export default async function BillingPage() {
  const overview = await accountGet<BillingOverview>("billing");

  return (
    <>
      <PageHeader title="Plan & billing" description="Prepaid 30-day plans via Airtel Money or TNM Mpamba. No auto-renewal." />
      {overview ? (
        <BillingPanel overview={overview} />
      ) : (
        <Notice tone="danger" title="Billing is temporarily unavailable">
          Your plan and keys are unaffected. Refresh in a moment.
        </Notice>
      )}
    </>
  );
}
