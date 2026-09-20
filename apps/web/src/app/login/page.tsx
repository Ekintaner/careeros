import { AuthForm } from "@/features/auth/auth-form";
export const metadata = { title: "Login" };
export default async function Page({
  searchParams,
}: {
  searchParams: Promise<{ password?: string }>;
}) {
  return (
    <AuthForm
      mode="login"
      passwordChanged={(await searchParams).password === "changed"}
    />
  );
}
