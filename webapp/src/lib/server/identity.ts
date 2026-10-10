import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import type { AccountUser, AuthConfig } from "@/lib/types/account";
import { identityBaseUrl, isProduction, sessionCookieName } from "./config";

type IdentityRequest = {
  method?: string;
  token?: string | null;
  body?: unknown;
  forwardedFor?: string | null;
  userAgent?: string | null;
};

export async function identityFetch(path: string, request: IdentityRequest = {}) {
  const headers = new Headers({ Accept: "application/json" });

  if (request.token) {
    headers.set("Authorization", `Bearer ${request.token}`);
  }

  if (request.body !== undefined) {
    headers.set("Content-Type", "application/json");
  }

  if (request.forwardedFor) {
    headers.set("X-Forwarded-For", request.forwardedFor);
  }

  if (request.userAgent) {
    headers.set("User-Agent", request.userAgent);
  }

  return fetch(`${identityBaseUrl}${path}`, {
    method: request.method ?? "GET",
    headers,
    body: request.body === undefined ? undefined : JSON.stringify(request.body),
    cache: "no-store",
  });
}

export async function getSessionToken() {
  return (await cookies()).get(sessionCookieName)?.value ?? null;
}

export async function getCurrentUser(): Promise<AccountUser | null> {
  const token = await getSessionToken();

  if (!token) {
    return null;
  }

  try {
    const response = await identityFetch("/api/v1/me", { token });
    return response.ok ? ((await response.json()) as AccountUser) : null;
  } catch {
    return null;
  }
}

// For dashboard server components: redirects to sign-in when the session is missing or revoked.
export async function requireUser(nextPath = "/dashboard") {
  const user = await getCurrentUser();

  if (!user) {
    redirect(`/sign-in?next=${encodeURIComponent(nextPath)}`);
  }

  return user;
}

export async function getAuthConfig(): Promise<AuthConfig> {
  try {
    const response = await identityFetch("/api/v1/auth/config");

    if (response.ok) {
      return (await response.json()) as AuthConfig;
    }
  } catch {
    // Identity unreachable: render the page with sign-in options disabled.
  }

  return { googleEnabled: false, googleClientId: null, emailEnabled: false, devSignInEnabled: false };
}

export function sessionCookieOptions(expiresAt: string) {
  return {
    httpOnly: true,
    secure: isProduction,
    sameSite: "lax" as const,
    path: "/",
    expires: new Date(expiresAt),
  };
}

export function clientAddress(headers: Headers) {
  return headers.get("x-forwarded-for")?.split(",")[0]?.trim() ?? headers.get("x-real-ip") ?? null;
}

// Server-side read for dashboard pages. Returns null when Identity or Billing cannot answer.
export async function accountGet<T>(path: string): Promise<T | null> {
  const token = await getSessionToken();

  if (!token) {
    return null;
  }

  try {
    const response = await identityFetch(`/api/v1/${path}`, { token });
    return response.ok ? ((await response.json()) as T) : null;
  } catch {
    return null;
  }
}
