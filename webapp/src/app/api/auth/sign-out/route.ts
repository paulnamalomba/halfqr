import { NextRequest, NextResponse } from "next/server";
import { sessionCookieName } from "@/lib/server/config";
import { identityFetch } from "@/lib/server/identity";
import { isSameOrigin } from "@/lib/server/same-origin";

export const dynamic = "force-dynamic";

export async function POST(request: NextRequest) {
  if (!isSameOrigin(request)) {
    return NextResponse.json({ title: "Cross-site request blocked." }, { status: 403 });
  }

  const token = request.cookies.get(sessionCookieName)?.value;

  if (token) {
    await identityFetch("/api/v1/auth/sign-out", { method: "POST", token }).catch(() => null);
  }

  const response = NextResponse.redirect(new URL("/sign-in?signed_out=1", request.url), { status: 303 });
  response.cookies.set(sessionCookieName, "", { path: "/", maxAge: 0 });
  return response;
}
