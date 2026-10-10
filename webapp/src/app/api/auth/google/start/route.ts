import { createHash, randomBytes } from "node:crypto";
import { NextRequest, NextResponse } from "next/server";
import { googleClientId, isProduction, resolveAppUrl } from "@/lib/server/config";

export const dynamic = "force-dynamic";

const oauthCookieOptions = { httpOnly: true, secure: isProduction, sameSite: "lax" as const, path: "/api/auth/google", maxAge: 600 };

// Starts Google OAuth (authorization code + PKCE). Stubbed until GOOGLE_OAUTH_CLIENT_ID is set.
export async function GET(request: NextRequest) {
  const intent = request.nextUrl.searchParams.get("intent") === "sign-up" ? "sign-up" : "sign-in";
  const next = sanitizeNext(request.nextUrl.searchParams.get("next"));

  if (!googleClientId) {
    return NextResponse.redirect(new URL(`/${intent}?error=google_not_configured`, request.url));
  }

  const state = randomBytes(24).toString("base64url");
  const codeVerifier = randomBytes(48).toString("base64url");
  const codeChallenge = createHash("sha256").update(codeVerifier).digest("base64url");
  const redirectUri = `${resolveAppUrl(request.nextUrl.origin)}/api/auth/google/callback`;

  const authorizeUrl = new URL("https://accounts.google.com/o/oauth2/v2/auth");
  authorizeUrl.search = new URLSearchParams({
    client_id: googleClientId,
    redirect_uri: redirectUri,
    response_type: "code",
    scope: "openid email profile",
    state,
    code_challenge: codeChallenge,
    code_challenge_method: "S256",
    prompt: "select_account",
  }).toString();

  const response = NextResponse.redirect(authorizeUrl);
  response.cookies.set("hqr_oauth_state", state, oauthCookieOptions);
  response.cookies.set("hqr_oauth_verifier", codeVerifier, oauthCookieOptions);
  response.cookies.set("hqr_oauth_next", next, oauthCookieOptions);
  response.cookies.set("hqr_oauth_intent", intent, oauthCookieOptions);
  return response;
}

// Only same-site dashboard paths are allowed as post-login destinations.
function sanitizeNext(value: string | null) {
  return value && value.startsWith("/dashboard") && !value.startsWith("//") ? value : "/dashboard";
}
