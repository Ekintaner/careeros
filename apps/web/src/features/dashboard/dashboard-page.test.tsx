import { render, screen, fireEvent } from "@testing-library/react";
import { beforeEach, expect, it, vi } from "vitest";
import { DashboardPage, type Dashboard } from "./dashboard-page";
const mocks = vi.hoisted(() => ({ request: vi.fn() }));
vi.mock("@/features/auth/auth-client", () => ({ authClient: () => mocks }));
const empty: Dashboard = {
  asOfUtc: "2026-10-17T12:00:00Z",
  totalApplications: 0,
  activeApplications: 0,
  offers: 0,
  savedJobs: 0,
  openTasks: 0,
  overdueTasks: 0,
  upcomingInterviews: 0,
  recentApplications: [],
  nextTasks: [],
  nextInterviews: [],
};
beforeEach(() => {
  mocks.request.mockReset();
});
it("uses recorded zeros and actionable empty states", async () => {
  mocks.request.mockResolvedValue(empty);
  render(<DashboardPage />);
  await screen.findByText("Your next step starts here");
  expect(
    screen.getByText("No applications yet. Track an existing job to begin."),
  ).toBeVisible();
  expect(screen.getByRole("link", { name: "Save a job" })).toHaveAttribute(
    "href",
    "/jobs/new",
  );
  expect(mocks.request).toHaveBeenCalledTimes(1);
});
it("shows owned current records without fabricated history", async () => {
  mocks.request.mockResolvedValue({
    ...empty,
    totalApplications: 3,
    activeApplications: 2,
    offers: 1,
    openTasks: 4,
    overdueTasks: 2,
    recentApplications: [
      { id: "app-1", title: "Engineer", company: "Company", status: "Offer" },
    ],
    nextInterviews: [
      {
        id: "i1",
        title: "Interview role",
        company: "Company",
        startUtc: "2026-10-18T12:00:00Z",
        endUtc: "2026-10-18T13:00:00Z",
      },
    ],
    nextTasks: [
      { id: "t1", title: "Follow up", dueUtc: null, priority: "High" },
    ],
  });
  render(<DashboardPage />);
  expect(await screen.findByRole("link", { name: /Engineer/ })).toHaveAttribute(
    "href",
    "/applications/app-1",
  );
  expect(screen.getByText("Offer")).toBeVisible();
  expect(screen.getByText("2 overdue")).toBeVisible();
  expect(screen.getByRole("link", { name: /Follow up/ })).toHaveAttribute(
    "href",
    "/tasks/t1",
  );
  expect(screen.queryByText("Your next step starts here")).toBeNull();
});
it("does not replace a failed load with zero statistics and can retry", async () => {
  mocks.request.mockRejectedValueOnce(new Error()).mockResolvedValue(empty);
  render(<DashboardPage />);
  await screen.findByRole("alert");
  expect(
    screen.queryByRole("region", { name: "Current workspace counts" }),
  ).toBeNull();
  fireEvent.click(screen.getByText("Reload overview"));
  await screen.findByText("Your next step starts here");
});
