import type { Metadata } from "next";
import { AppShell } from "@/components/layout/app-shell";
import "@/styles/globals.css";

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
        <AppShell>{children}</AppShell>
      </body>
    </html>
  );
}
