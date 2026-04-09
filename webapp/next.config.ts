import type { NextConfig } from "next";

const publicApiBaseUrl = (process.env.HAVEQR_PUBLIC_API_BASE_URL ?? "http://127.0.0.1:5080").replace(/\/$/, "");

const nextConfig: NextConfig = {
  reactStrictMode: true,
  async rewrites() {
    return [
      {
        source: "/api/v1/qr/:path*",
        destination: `${publicApiBaseUrl}/api/v1/qr/:path*`,
      },
    ];
  },
};

export default nextConfig;