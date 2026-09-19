import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { AppShell } from "./app-shell";
import OverviewPage from "@/app/page";

describe("workspace shell", () => {
  it("shows an honest overview and marks future tools unavailable", () => {
    render(
      <AppShell>
        <OverviewPage />
      </AppShell>,
    );
    expect(
      screen.getByRole("heading", { name: "Overview", level: 1 }),
    ).toBeVisible();
    expect(screen.getByText("No applications to display")).toBeVisible();
    const nav = within(
      screen.getByRole("navigation", { name: "Main navigation" }),
    );
    expect(nav.getByRole("link", { name: "Overview" })).toHaveAttribute(
      "aria-current",
      "page",
    );
    expect(nav.getByRole("button", { name: "Applications" })).toBeDisabled();
    expect(
      screen.getByRole("link", { name: "Skip to content" }),
    ).toHaveAttribute("href", "#main-content");
  });
});
