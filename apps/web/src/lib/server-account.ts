import "server-only";
import { cookies } from "next/headers";
import type { Account } from "@/features/auth/auth-client";

export async function serverAccount(): Promise<Account | null> {
  const origin = process.env.API_INTERNAL_URL;
  if (!origin)
    throw new Error("API_INTERNAL_URL is required on the web server.");
  const jar = await cookies();
  const access =
    jar.get("__Host-careeros-access") ?? jar.get("careeros-access");
  if (!access) return null;
  const response = await fetch(new URL("/api/v1/auth/me", origin), {
    headers: { Cookie: `${access.name}=${access.value}` },
    cache: "no-store",
  });
  if (response.status === 401) return null;
  if (!response.ok) throw new Error("Account service is unavailable.");
  return response.json() as Promise<Account>;
}
