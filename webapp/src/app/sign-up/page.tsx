import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { AuthHero } from "@/components/auth/auth-hero";
import { AuthPanel } from "@/components/auth/auth-panel";
import { getAuthConfig, getCurrentUser } from "@/lib/server/identity";
import "../console.css";

export const metadata: Metadata = { title: "Create account" };
export const dynamic = "force-dynamic";

export default async function Page({ searchParams }: { searchParams: Promise<Record<string, string | undefined>> }) {
  const params = await searchParams;
  const next = params.next?.startsWith("/dashboard") ? params.next : undefined;

  if (await getCurrentUser()) {
    redirect(next ?? "/dashboard");
  }

  return (
    <div className="auth-layout console">
      <AuthHero />
      <AuthPanel intent="sign-up" config={await getAuthConfig()} error={params.error} next={next} signedOut={params.signed_out === "1"} />
    </div>
  );
}
