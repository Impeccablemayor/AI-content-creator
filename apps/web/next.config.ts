import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  reactCompiler: true,
  async rewrites() {
    const apiOrigin = (process.env.MAYORX_API_ORIGIN ?? "http://localhost:5025").replace(
      /\/+$/,
      "",
    );

    return [
      {
        source: "/api/v1/:path*",
        destination: `${apiOrigin}/api/v1/:path*`,
      },
    ];
  },
};

export default nextConfig;
