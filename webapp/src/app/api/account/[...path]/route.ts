import { NextRequest, NextResponse } from "next/server";
import { sessionCookieName } from "@/lib/server/config";
import { identityFetch } from "@/lib/server/identity";
import { isSameOrigin } from "@/lib/server/same-origin";

export const dynamic = "force-dynamic";

// Backend-for-frontend: the session cookie never reaches browser JavaScript; it is attached here as a bearer token.
const allowedRoots = new Set(["me", "sessions", "credentials", "billing"]);
const maxBodyBytes = 16 * 1024;

async function relay(request: NextRequest, context: { params: Promise<{ path: string[] }> }) {
  const { path } = await context.params;

  if (!allowedRoots.has(path[0] ?? "") || path.some((segment) => segment === ".." || segment.includes("/"))) {
    return NextResponse.json({ title: "Not found." }, { status: 404 });
  }

  if (request.method !== "GET" && !isSameOrigin(request)) {
    return NextResponse.json({ title: "Cross-site request blocked." }, { status: 403 });
  }

  const token = request.cookies.get(sessionCookieName)?.value;

  if (!token) {
    return NextResponse.json({ title: "Sign in required." }, { status: 401 });
  }

  let body: unknown;

  if (request.method === "POST") {
    const text = await request.text();

    if (text.length > maxBodyBytes) {
      return NextResponse.json({ title: "Request body too large." }, { status: 413 });
    }

    try {
      body = text ? JSON.parse(text) : undefined;
    } catch {
      return NextResponse.json({ title: "Request body must be JSON." }, { status: 400 });
    }
  }

  const upstream = await identityFetch(`/api/v1/${path.map(encodeURIComponent).join("/")}${request.nextUrl.search}`, {
    method: request.method,
    token,
    body,
  }).catch(() => null);

  if (!upstream) {
    return NextResponse.json({ title: "Account service is unavailable." }, { status: 503 });
  }

  return new NextResponse(upstream.status === 204 ? null : await upstream.text(), {
    status: upstream.status,
    headers: { "Content-Type": upstream.headers.get("content-type") ?? "application/json", "Cache-Control": "no-store" },
  });
}

export { relay as GET, relay as POST, relay as DELETE };
