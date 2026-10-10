import { NextRequest, NextResponse } from "next/server";
import { clientAddress, identityFetch } from "@/lib/server/identity";
import { isSameOrigin } from "@/lib/server/same-origin";

export const dynamic = "force-dynamic";

// Sends a one-time sign-in link through Identity (delivered by Resend).
export async function POST(request: NextRequest) {
  if (!isSameOrigin(request)) {
    return NextResponse.json({ title: "Cross-site request blocked." }, { status: 403 });
  }

  const body = (await request.json().catch(() => null)) as { email?: string } | null;

  const response = await identityFetch("/api/v1/auth/email/start", {
    method: "POST",
    body: { email: body?.email ?? "" },
    forwardedFor: clientAddress(request.headers),
  }).catch(() => null);

  if (!response) {
    return NextResponse.json({ title: "Sign-in is temporarily unavailable." }, { status: 503 });
  }

  return new NextResponse(await response.text(), {
    status: response.status,
    headers: { "Content-Type": response.headers.get("content-type") ?? "application/json" },
  });
}
