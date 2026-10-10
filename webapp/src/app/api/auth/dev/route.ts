import { NextRequest, NextResponse } from "next/server";
import { isProduction, sessionCookieName } from "@/lib/server/config";
import { clientAddress, identityFetch, sessionCookieOptions } from "@/lib/server/identity";
import { isSameOrigin } from "@/lib/server/same-origin";

export const dynamic = "force-dynamic";

// Local development only. Identity also refuses this unless Identity:EnableDevSignIn is true.
export async function POST(request: NextRequest) {
  if (isProduction || !isSameOrigin(request)) {
    return NextResponse.json({ title: "Not found." }, { status: 404 });
  }

  const body = (await request.json().catch(() => null)) as { email?: string; displayName?: string } | null;

  const response = await identityFetch("/api/v1/auth/dev", {
    method: "POST",
    body: { email: body?.email ?? "", displayName: body?.displayName },
    forwardedFor: clientAddress(request.headers),
    userAgent: request.headers.get("user-agent"),
  }).catch(() => null);

  if (!response?.ok) {
    return new NextResponse(response ? await response.text() : JSON.stringify({ title: "Identity is unavailable." }), {
      status: response?.status ?? 503,
      headers: { "Content-Type": "application/json" },
    });
  }

  const session = (await response.json()) as { sessionToken: string; expiresAt: string };
  const result = NextResponse.json({ ok: true });
  result.cookies.set(sessionCookieName, session.sessionToken, sessionCookieOptions(session.expiresAt));
  return result;
}
