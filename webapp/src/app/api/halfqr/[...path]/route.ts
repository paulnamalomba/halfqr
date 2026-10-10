import { NextRequest } from "next/server";

const upstreamBaseUrl = (process.env.HALFQR_API_BASE_URL ?? process.env.NEXT_PUBLIC_HALFQR_API_BASE_URL ?? "https://api.halfqr.com")
  .trim()
  .replace(/\/$/, "");

const proxySecret = (process.env.HALFQR_PROXY_SECRET ?? "").trim();
const proxyPrefix = "/api/halfqr";

// Largest legitimate body is a 512 KB raster logo as base64 plus options. PublicApi enforces the same cap.
const maxBodyBytes = 1024 * 1024;

// Only these request headers reach the API. Cookies and credentials from the browser are never forwarded.
const forwardedRequestHeaders = ["accept", "content-type"];
const forwardedResponseHeaders = ["content-type", "content-disposition", "cache-control", "location", "retry-after", "content-security-policy"];

export const dynamic = "force-dynamic";
export const revalidate = 0;

export async function GET(request: NextRequest) {
  return proxyRequest(request);
}

export async function POST(request: NextRequest) {
  return proxyRequest(request);
}

async function proxyRequest(request: NextRequest) {
  const upstreamPath = request.nextUrl.pathname.slice(proxyPrefix.length);

  if (!upstreamPath.startsWith("/api/v1/qr/")) {
    return Response.json({ title: "Not found." }, { status: 404 });
  }

  const declaredLength = Number(request.headers.get("content-length") ?? "0");

  if (declaredLength > maxBodyBytes) {
    return Response.json({ title: "Request body too large." }, { status: 413 });
  }

  const body = canIncludeBody(request.method) ? await request.arrayBuffer() : undefined;

  if (body && body.byteLength > maxBodyBytes) {
    return Response.json({ title: "Request body too large." }, { status: 413 });
  }

  const upstreamHeaders = pickHeaders(request.headers, forwardedRequestHeaders);
  const browserAddress = request.headers.get("x-forwarded-for")?.split(",")[0]?.trim();

  // PublicApi only trusts X-Forwarded-For for rate limiting when the shared proxy secret is present.
  if (proxySecret && browserAddress) {
    upstreamHeaders.set("X-Forwarded-For", browserAddress);
    upstreamHeaders.set("X-HalfQR-Proxy-Secret", proxySecret);
  }

  const upstreamResponse = await fetch(`${upstreamBaseUrl}${upstreamPath}${request.nextUrl.search}`, {
    method: request.method,
    headers: upstreamHeaders,
    body,
    cache: "no-store",
    redirect: "manual",
  });

  return new Response(upstreamResponse.body, {
    status: upstreamResponse.status,
    statusText: upstreamResponse.statusText,
    headers: pickHeaders(upstreamResponse.headers, forwardedResponseHeaders),
  });
}

function canIncludeBody(method: string) {
  return method !== "GET" && method !== "HEAD";
}

function pickHeaders(source: Headers, allowed: string[]) {
  const headers = new Headers();

  for (const name of allowed) {
    const value = source.get(name);

    if (value) {
      headers.set(name, value);
    }
  }

  return headers;
}
