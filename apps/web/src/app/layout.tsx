import type { Metadata } from "next";
import { AuthProvider } from "@/features/auth/auth-provider";
import "@/styles/globals.css";
import "@/styles/applications.css";
import "@/styles/product.css";

export const metadata: Metadata = {
  title: { default: "Overview · CareerOS", template: "%s · CareerOS" },
  description: "A considered workspace for your next career move.",
  robots: { index: false, follow: false },
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en">
      <body>
        <AuthProvider>{children}</AuthProvider>
      </body>
    </html>
  );
}
