import { render, screen, fireEvent } from "@testing-library/react";
import { expect, it, vi } from "vitest";
import { NavigationLink } from "./navigation-link";
vi.mock("next/navigation", () => ({ usePathname: () => "/tasks/record" }));
it("marks parent routes current and closes the mobile menu on navigation", () => {
  const r = render(
    <details className="mobile-nav" open>
      <summary>Menu</summary>
      <NavigationLink href="/tasks">Tasks</NavigationLink>
    </details>,
  );
  const link = screen.getByRole("link", { name: "Tasks" });
  expect(link).toHaveAttribute("aria-current", "page");
  link.addEventListener("click", (e) => e.preventDefault());
  fireEvent.click(link);
  expect(r.container.querySelector("details")).not.toHaveAttribute("open");
});
