import type { ReactNode } from "react";
import { PageHeader } from "@/components/console/ui";
import { accountGet } from "@/lib/server/identity";
import type { ApiCredential, BillingOverview, CredentialKind, CredentialOptions } from "@/lib/types/account";
import { CredentialsManager } from "./credentials-manager";

const fallbackOptions: CredentialOptions = { scopes: ["qr:render", "qr:read"], accessTokenLifetimesDays: [1, 7, 30, 90], apiKeyLifetimesDays: [30, 90, 180, 365] };

// Server component shared by the API keys and access tokens pages.
export async function CredentialsPage({ kind, title, description }: { kind: CredentialKind; title: string; description: ReactNode }) {
  const [credentials, options, billing] = await Promise.all([
    accountGet<ApiCredential[]>("credentials"),
    accountGet<CredentialOptions>("credentials/options"),
    accountGet<BillingOverview>("billing"),
  ]);

  return (
    <>
      <PageHeader title={title} description={description} />
      <CredentialsManager kind={kind} initialCredentials={credentials ?? []} options={options ?? fallbackOptions} entitlement={billing?.entitlement ?? null} />
    </>
  );
}
