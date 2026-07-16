/** @type {import('next').NextConfig} */
const path = require("path");

const backendUrl = (process.env.NEXT_PUBLIC_BACKEND_URL || "http://localhost:5180").replace(
  /\/+$/,
  "",
);

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
    return [
      {
        source: "/api/:path*",
        destination: `${backendUrl}/api/:path*`,
      },
    ];
  },
  webpack: (config) => {
    config.resolve.alias = {
      ...config.resolve.alias,
      reselect: path.resolve(__dirname, "node_modules/reselect"),
    };
    return config;
  },
};

module.exports = nextConfig;
