/** @type {import('next').NextConfig} */
const path = require("path");

const rawBackendUrl = process.env.NEXT_PUBLIC_BACKEND_URL?.trim();
const backendUrl = (rawBackendUrl || "http://localhost:5180").replace(/\/+$/, "");

// Fail the Vercel/production build if the API origin was never set (would rewrite to localhost).
if (process.env.VERCEL && !rawBackendUrl) {
  throw new Error(
    "NEXT_PUBLIC_BACKEND_URL is required on Vercel (e.g. https://your-api.onrender.com).",
  );
}

const nextConfig = {
  reactStrictMode: true,
  outputFileTracingRoot: path.join(__dirname),
  experimental: {
    optimizePackageImports: [
      "@phosphor-icons/react",
      "lucide-react",
      "date-fns",
      "recharts",
    ],
  },
  async rewrites() {
    // Same-origin /api proxy for local/dev. When NEXT_PUBLIC_BACKEND_URL is set,
    // the client calls that origin directly (see src/lib/api.js).
    if (rawBackendUrl) return [];
    return [
      {
        source: "/api/:path*",
        destination: `${backendUrl}/api/:path*`,
      },
    ];
  },
};

module.exports = nextConfig;
