import { NextResponse } from "next/server";

const PUBLIC_PATHS = ["/login"];

export function middleware(request) {
  const { pathname } = request.nextUrl;

  if (
    PUBLIC_PATHS.some((path) => pathname === path || pathname.startsWith(`${path}/`))
    || pathname.startsWith("/_next")
    || pathname.startsWith("/api")
    || pathname.includes(".")
  ) {
    return NextResponse.next();
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.ico).*)"],
};
