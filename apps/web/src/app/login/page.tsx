import { AuthForm } from "@/features/auth/auth-form";
export const metadata = { title: "Login" };
export default async function Page({
  searchParams,
}: {
  searchParams: Promise<{ password?: string; returnTo?: string }>;
}) {
  return (
    <AuthForm
      mode="login"
      returnTo={(await searchParams).returnTo}
      passwordChanged={(await searchParams).password === "changed"}
    />
  );
}
