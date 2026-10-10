import type { Metadata } from "next";
import { CredentialsPage } from "@/components/dashboard/credentials-page";

export const metadata: Metadata = { title: "API keys" };

export default function ApiKeysPage() {
  return (
    <CredentialsPage
      kind="ApiKey"
      title="API keys"
      description={
        <>
          Send as <span className="mono">Authorization: Bearer</span> or <span className="mono">X-Api-Key</span>.
        </>
      }
    />
  );
}
