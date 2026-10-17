import { SessionRecovery } from "@/features/auth/auth-provider";
export default async function SessionPage({
  searchParams,
}: {
  searchParams: Promise<{ returnTo?: string }>;
}) {
  return <SessionRecovery returnTo={(await searchParams).returnTo} />;
}
