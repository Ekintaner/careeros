"use client";
import Link from "next/link";
import { usePathname } from "next/navigation";
export function NavigationLink({
  href,
  children,
}: {
  href: string;
  children: React.ReactNode;
}) {
  const current = usePathname() === href;
  return (
    <Link
      href={href}
      className={`nav-item${current ? " active" : ""}`}
      aria-current={current ? "page" : undefined}
    >
      {children}
    </Link>
  );
}
export function Breadcrumb() {
  const path = usePathname();
  return (
    <span className="breadcrumb">
      Workspace <span>/</span>{" "}
      <strong>
        {path === "/profile"
          ? "Career profile"
          : path === "/settings"
            ? "Settings"
            : "Overview"}
      </strong>
    </span>
  );
}
