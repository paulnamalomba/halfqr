import type { Metadata } from "next";
import { CredentialsPage } from "@/components/dashboard/credentials-page";

export const metadata: Metadata = { title: "Access tokens" };

export default function AccessTokensPage() {
  return <CredentialsPage kind="AccessToken" title="Access tokens" description="Short-lived tokens for scripts and testing." />;
}
