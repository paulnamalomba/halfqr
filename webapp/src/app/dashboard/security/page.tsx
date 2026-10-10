import type { Metadata } from "next";
import { PageHeader, SectionCard } from "@/components/console/ui";
import { SessionsPanel } from "@/components/dashboard/sessions-panel";
import { formatDate, formatDateTime } from "@/lib/format";
import { accountGet, requireUser } from "@/lib/server/identity";
import type { AccountSession } from "@/lib/types/account";

export const metadata: Metadata = { title: "Sessions & security" };

const providerLabels: Record<string, string> = { google: "Google", email: "Email link", dev: "Development sign-in" };

export default async function SecurityPage() {
  const user = await requireUser("/dashboard/security");
  const sessions = (await accountGet<AccountSession[]>("sessions")) ?? [];
  const details: Array<[string, string]> = [
    ["Name", user.displayName],
    ["Email", `${user.email} (verified)`],
    ["Sign-in method", providerLabels[user.provider] ?? user.provider],
    ["Member since", formatDate(user.createdAt)],
    ["Last sign-in", formatDateTime(user.lastSignInAt)],
  ];

  return (
    <>
      <PageHeader title="Sessions & security" description="Where you are signed in and how your account authenticates." />

      <SectionCard title="Account" description="Passwordless sign-in. There is no password to leak." padded>
        <dl className="definition-list">
          {details.map(([label, value]) => (
            <div key={label} style={{ display: "contents" }}>
              <dt>{label}</dt>
              <dd>{value}</dd>
            </div>
          ))}
        </dl>
      </SectionCard>

      <SessionsPanel initialSessions={sessions} />
    </>
  );
}
