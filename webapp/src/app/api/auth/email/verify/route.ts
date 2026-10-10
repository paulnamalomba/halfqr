import { NextRequest, NextResponse } from "next/server";
import { sessionCookieName } from "@/lib/server/config";
import { clientAddress, identityFetch, sessionCookieOptions } from "@/lib/server/identity";

export const dynamic = "force-dynamic";

// Target of the emailed link. Exchanges the one-time token for a dashboard session.
export async function GET(request: NextRequest) {
  const token = request.nextUrl.searchParams.get("token") ?? "";

  const response = await identityFetch("/api/v1/auth/email/verify", {
    method: "POST",
    body: { token },
    forwardedFor: clientAddress(request.headers),
    userAgent: request.headers.get("user-agent"),
  }).catch(() => null);

  if (!response?.ok) {
    const problem = response ? await response.json().catch(() => null) : null;
    const error = problem?.type === "email_domain_blocked" ? "email_domain_blocked" : "email_link_invalid";
    return NextResponse.redirect(new URL(`/sign-in?error=${error}`, request.url));
  }

  const session = (await response.json()) as { sessionToken: string; expiresAt: string };
  const redirect = NextResponse.redirect(new URL("/dashboard", request.url));
  redirect.cookies.set(sessionCookieName, session.sessionToken, sessionCookieOptions(session.expiresAt));
  return redirect;
}
