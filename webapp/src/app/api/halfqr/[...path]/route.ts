import { NextRequest } from "next/server";

const upstreamBaseUrl = (process.env.HALFQR_API_BASE_URL ?? process.env.NEXT_PUBLIC_HALFQR_API_BASE_URL ?? "https://halfqr-api-demo.computemore.com")
  .trim()
  .replace(/\/$/, "");

const proxyPrefix = "/api/halfqr";
const hopByHopHeaders = new Set([
  "connection",
  "content-encoding",
  "content-length",
  "host",
  "keep-alive",
  "proxy-authenticate",
  "proxy-authorization",
  "te",
  "trailer",
  "transfer-encoding",
  "upgrade",
]);

export const dynamic = "force-dynamic";
export const revalidate = 0;

export async function GET(request: NextRequest) {
  return proxyRequest(request);
}

export async function POST(request: NextRequest) {
  return proxyRequest(request);
}

async function proxyRequest(request: NextRequest) {
  const upstreamUrl = new URL(`${upstreamBaseUrl}${request.nextUrl.pathname.slice(proxyPrefix.length)}${request.nextUrl.search}`);
  const upstreamHeaders = copyHeaders(request.headers);
  upstreamHeaders.delete("origin");
  upstreamHeaders.delete("referer");

  const upstreamResponse = await fetch(upstreamUrl, {
    method: request.method,
    headers: upstreamHeaders,
    body: canIncludeBody(request.method) ? await request.arrayBuffer() : undefined,
    cache: "no-store",
    redirect: "manual",
  });

  return new Response(upstreamResponse.body, {
    status: upstreamResponse.status,
    statusText: upstreamResponse.statusText,
    headers: copyHeaders(upstreamResponse.headers),
  });
}

function canIncludeBody(method: string) {
  return method !== "GET" && method !== "HEAD";
}

function copyHeaders(source: Headers) {
  const headers = new Headers();

  source.forEach((value, key) => {
    if (!hopByHopHeaders.has(key.toLowerCase())) {
      headers.set(key, value);
    }
  });

  return headers;
}