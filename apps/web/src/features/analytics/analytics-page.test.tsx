import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import { it, expect, vi, beforeEach } from "vitest";
import { AnalyticsPage } from "./analytics-page";
const mocks = vi.hoisted(() => ({ request: vi.fn() }));
vi.mock("@/features/opportunities/client", () => ({
  request: mocks.request,
  failure: () => "Unavailable",
}));
beforeEach(() => {
  mocks.request.mockReset();
});
it("shows truthful zero denominators and empty history", async () => {
  mocks.request.mockResolvedValue({
    startUtc: "2026-10-01T00:00:00Z",
    endExclusiveUtc: "2026-10-18T00:00:00Z",
    total: 0,
    applied: 0,
    interviewed: 0,
    offered: 0,
    timeZone: "UTC",
    asOfUtc: "2026-10-17T00:00:00Z",
    stages: [],
    trends: [],
    companies: [],
    roles: [],
    durations: [],
    interviews: [],
    tasks: [],
  });
  render(<AnalyticsPage />);
  await screen.findByText(/No applications created/);
  expect(screen.getByText(/Interview conversion:/)).toHaveTextContent(
    "N/A (0/0)",
  );
  expect(screen.getByText("No completed stage visits recorded.")).toBeVisible();
});
it("shows a recoverable error", async () => {
  mocks.request.mockRejectedValue(new Error());
  render(<AnalyticsPage />);
  await screen.findByRole("alert");
  fireEvent.click(screen.getByText("Retry"));
  await waitFor(() => expect(mocks.request).toHaveBeenCalledTimes(2));
});
it("renders recorded conversions and submits calendar filters", async () => {
  mocks.request.mockResolvedValue({
    startUtc: "2026-10-01T00:00:00Z",
    endExclusiveUtc: "2026-10-18T00:00:00Z",
    total: 4,
    applied: 2,
    interviewed: 1,
    offered: 0,
    timeZone: "UTC",
    asOfUtc: "2026-10-17T00:00:00Z",
    stages: [{ label: "Rejected", count: 1 }],
    trends: [],
    companies: [{ label: "<script>company</script>", count: 4 }],
    roles: [],
    durations: [{ label: "Applied", count: 2, averageDays: 1.25 }],
    interviews: [],
    tasks: [],
  });
  render(<AnalyticsPage />);
  await screen.findByText(/Interview conversion:/);
  expect(screen.getByText(/Interview conversion:/)).toHaveTextContent(
    "50.0% (1/2)",
  );
  expect(screen.getByText("1.25")).toBeVisible();
  expect(screen.getByText("<script>company</script>")).toBeVisible();
  fireEvent.change(screen.getByLabelText("From (inclusive)"), {
    target: { value: "2026-10-01" },
  });
  fireEvent.change(screen.getByLabelText("Through (inclusive)"), {
    target: { value: "2026-10-17" },
  });
  fireEvent.change(screen.getByLabelText("Time zone"), {
    target: { value: "Europe/Kyiv" },
  });
  fireEvent.change(screen.getByLabelText("Trend interval"), {
    target: { value: "month" },
  });
  fireEvent.click(screen.getByText("Apply filters"));
  await waitFor(() =>
    expect(mocks.request.mock.calls.at(-1)?.[0]).toBe(
      "analytics?from=2026-10-01&to=2026-10-17&timeZone=Europe%2FKyiv&interval=month",
    ),
  );
});
