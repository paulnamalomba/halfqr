import type { NextRequest } from "next/server";

// CSRF guard for cookie-authenticated mutations: the browser's Origin must match this host.
export function isSameOrigin(request: NextRequest) {
  const origin = request.headers.get("origin");

  if (!origin) {
    return false;
  }

  try {
    const host = request.headers.get("x-forwarded-host") ?? request.headers.get("host");
    return new URL(origin).host === host;
  } catch {
    return false;
  }
}
