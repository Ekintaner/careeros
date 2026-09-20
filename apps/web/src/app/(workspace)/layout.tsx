import { redirect } from "next/navigation";
import { serverAccount } from "@/lib/server-account";
import { AppShell } from "@/components/layout/app-shell";
import { AuthGate } from "@/features/auth/auth-provider";

export default async function WorkspaceLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const account = await serverAccount();
  if (!account) redirect("/session");
  return (
    <AuthGate>
      <AppShell>{children}</AppShell>
    </AuthGate>
  );
}
