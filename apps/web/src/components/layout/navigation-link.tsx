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
  const path = usePathname();
  const current =
    path === href || (href !== "/" && path.startsWith(href + "/"));
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
        {path.startsWith("/analytics")
          ? "Analytics"
          : path.startsWith("/interviews")
            ? "Interviews"
            : path.startsWith("/tasks")
              ? "Tasks"
              : path.startsWith("/skills")
                ? "Skills intelligence"
                : path.startsWith("/applications")
                  ? "Applications"
                  : path.startsWith("/companies")
                    ? "Companies"
                    : path.startsWith("/jobs")
                      ? "Jobs"
                      : path.startsWith("/resumes")
                        ? "Resumes"
                        : path.startsWith("/documents")
                          ? "Documents"
                          : path === "/profile"
                            ? "Career profile"
                            : path === "/settings"
                              ? "Settings"
                              : "Overview"}
      </strong>
    </span>
  );
}
