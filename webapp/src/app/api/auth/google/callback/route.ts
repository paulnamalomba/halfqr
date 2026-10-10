import { timingSafeEqual } from "node:crypto";
import { NextRequest, NextResponse } from "next/server";
import { resolveAppUrl, sessionCookieName } from "@/lib/server/config";
import { clientAddress, identityFetch, sessionCookieOptions } from "@/lib/server/identity";

export const dynamic = "force-dynamic";

export async function GET(request: NextRequest) {
  const intent = request.cookies.get("hqr_oauth_intent")?.value === "sign-up" ? "sign-up" : "sign-in";
  const failure = (error: string) => clearOAuthCookies(NextResponse.redirect(new URL(`/${intent}?error=${error}`, request.url)));

  const code = request.nextUrl.searchParams.get("code");
  const state = request.nextUrl.searchParams.get("state");
  const expectedState = request.cookies.get("hqr_oauth_state")?.value;
  const codeVerifier = request.cookies.get("hqr_oauth_verifier")?.value;

  if (request.nextUrl.searchParams.get("error")) {
    return failure("google_cancelled");
  }

  if (!code || !state || !expectedState || !codeVerifier || !safeEqual(state, expectedState)) {
    return failure("google_state_mismatch");
  }

  const response = await identityFetch("/api/v1/auth/google/exchange", {
    method: "POST",
    body: { code, codeVerifier, redirectUri: `${resolveAppUrl(request.nextUrl.origin)}/api/auth/google/callback` },
    forwardedFor: clientAddress(request.headers),
    userAgent: request.headers.get("user-agent"),
  }).catch(() => null);

  if (!response?.ok) {
    const problem = response ? await response.json().catch(() => null) : null;
    return failure(problem?.type === "email_domain_blocked" ? "email_domain_blocked" : "google_failed");
  }

  const session = (await response.json()) as { sessionToken: string; expiresAt: string };
  const next = request.cookies.get("hqr_oauth_next")?.value ?? "/dashboard";
  const redirect = NextResponse.redirect(new URL(next.startsWith("/dashboard") ? next : "/dashboard", request.url));
  redirect.cookies.set(sessionCookieName, session.sessionToken, sessionCookieOptions(session.expiresAt));
  return clearOAuthCookies(redirect);
}

function clearOAuthCookies(response: NextResponse) {
  for (const name of ["hqr_oauth_state", "hqr_oauth_verifier", "hqr_oauth_next", "hqr_oauth_intent"]) {
    response.cookies.set(name, "", { path: "/api/auth/google", maxAge: 0 });
  }

  return response;
}

function safeEqual(left: string, right: string) {
  const a = Buffer.from(left);
  const b = Buffer.from(right);
  return a.length === b.length && timingSafeEqual(a, b);
}
