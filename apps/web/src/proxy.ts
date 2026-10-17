import { NextResponse, type NextRequest } from "next/server";

export function proxy(request: NextRequest) {
  const headers = new Headers(request.headers);
  // Always overwrite client input. This is routing context, never an auth decision.
  headers.set(
    "x-careeros-path",
    request.nextUrl.pathname + request.nextUrl.search,
  );
  return NextResponse.next({ request: { headers } });
}
export const config = { matcher: ["/((?!api/|_next/|favicon.ico).*)"] };
