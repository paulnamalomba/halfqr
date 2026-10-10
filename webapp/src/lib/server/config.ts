// Server-only settings. None of these reach the browser bundle.
export const identityBaseUrl = (process.env.HALFQR_IDENTITY_BASE_URL ?? "http://localhost:5240").trim().replace(/\/$/, "");

export const googleClientId = (process.env.GOOGLE_OAUTH_CLIENT_ID ?? "").trim();

export const proxySecret = (process.env.HALFQR_PROXY_SECRET ?? "").trim();

export const sessionCookieName = "hqr_session";

export const isProduction = process.env.NODE_ENV === "production";

// Public origin used for OAuth redirect URIs. Falls back to the request origin in development.
export function resolveAppUrl(requestOrigin: string) {
  return (process.env.HALFQR_APP_URL ?? requestOrigin).trim().replace(/\/$/, "");
}
