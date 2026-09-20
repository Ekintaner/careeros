import { AuthForm } from "@/features/auth/auth-form";
export const metadata = { title: "Reset Password" };
export default function Page() {
  return <AuthForm mode="reset-password" />;
}
