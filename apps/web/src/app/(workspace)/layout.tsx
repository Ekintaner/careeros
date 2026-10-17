import { redirect } from "next/navigation";
import { headers } from "next/headers";
import { safeReturnTo } from "@/features/auth/return-to";
import { serverAccount } from "@/lib/server-account";
import { AppShell } from "@/components/layout/app-shell";
import { AuthGate } from "@/features/auth/auth-provider";

export default async function WorkspaceLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const account = await serverAccount();
  if (!account) {
    const target = safeReturnTo((await headers()).get("x-careeros-path"));
    redirect(
      target === "/"
        ? "/session"
        : `/session?returnTo=${encodeURIComponent(target)}`,
    );
  }
  return (
    <AuthGate>
      <AppShell>{children}</AppShell>
    </AuthGate>
  );
}
