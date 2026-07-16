import { NextResponse } from "next/server";

const PUBLIC_PATHS = [
  "/login",
  "/signup",
  "/registration-received",
  "/platform/login",
  "/platform/tenants",
];

const readTenantSlug = (host) => {
  if (!host) return null;
  const parts = host.split(".");
  if (parts.length < 3) return null;
  const sub = parts[0].toLowerCase();
  if (["www", "app", "api", "localhost"].includes(sub)) return null;
  return sub.endsWith("-local") ? sub.slice(0, -6) : sub;
};

export function middleware(request) {
  const { pathname } = request.nextUrl;

  if (
    PUBLIC_PATHS.some((path) => pathname === path || pathname.startsWith(`${path}/`))
    || pathname.startsWith("/_next")
    || pathname.startsWith("/api")
    || pathname.includes(".")
  ) {
    const response = NextResponse.next();
    const slug = readTenantSlug(request.headers.get("host") || request.nextUrl.hostname);
    if (slug) response.headers.set("x-tenant-slug", slug);
    return response;
  }

  const response = NextResponse.next();
  const slug = readTenantSlug(request.headers.get("host") || request.nextUrl.hostname);
  if (slug) response.headers.set("x-tenant-slug", slug);
  return response;
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.ico).*)"],
};
