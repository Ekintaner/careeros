import Link from "next/link";
import { render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { AppShell } from "./app-shell";

vi.mock("next/navigation", () => ({ usePathname: () => "/" }));

vi.mock("@/features/auth/auth-provider", () => ({
  AccountMenu: () => <span>Test account</span>,
}));

describe("workspace shell", () => {
  it("exposes available navigation and overview landmarks", () => {
    render(
      <AppShell>
        <h1>Overview</h1>
        <Link href="/applications">Open your application pipeline →</Link>
      </AppShell>,
    );
    expect(
      screen.getByRole("heading", { name: "Overview", level: 1 }),
    ).toBeVisible();
    expect(
      screen.getByRole("link", { name: "Open your application pipeline →" }),
    ).toHaveAttribute("href", "/applications");
    const nav = within(
      screen.getByRole("navigation", { name: "Main navigation" }),
    );
    expect(nav.getByRole("link", { name: "Overview" })).toHaveAttribute(
      "aria-current",
      "page",
    );
    expect(nav.getByRole("link", { name: "Applications" })).toHaveAttribute(
      "href",
      "/applications",
    );
    expect(
      nav.getByRole("link", { name: "Skills intelligence" }),
    ).toHaveAttribute("href", "/skills");
    expect(nav.getByRole("link", { name: "Interviews" })).toHaveAttribute(
      "href",
      "/interviews",
    );
    expect(
      screen.getByRole("link", { name: "Skip to content" }),
    ).toHaveAttribute("href", "#main-content");
  });
});
