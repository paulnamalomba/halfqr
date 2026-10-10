import { NextRequest, NextResponse } from "next/server";

// Optimistic check only: the dashboard layout still validates the session with Identity on every request.
export function proxy(request: NextRequest) {
  if (!request.cookies.has("hqr_session")) {
    const signIn = new URL("/sign-in", request.url);
    signIn.searchParams.set("next", request.nextUrl.pathname);
    return NextResponse.redirect(signIn);
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/dashboard/:path*"],
};
