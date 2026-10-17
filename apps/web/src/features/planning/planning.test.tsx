import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, expect, it, vi } from "vitest";
import { PlanningList } from "./list";
import { PlanningEditor } from "./editor";
import { InterviewNotes } from "./notes";
import { inputTime, utcTime } from "./types";
import { ApiError } from "@/lib/api-client";
const mocks = vi.hoisted(() => ({
  request: vi.fn(),
  push: vi.fn(),
  replace: vi.fn(),
}));
vi.mock("@/features/auth/auth-client", () => ({ authClient: () => mocks }));
vi.mock("next/navigation", () => ({ useRouter: () => mocks }));
const task = {
  id: "task1",
  applicationId: "app1",
  title: "Follow up",
  description: "Ask about next steps",
  dueAtUtc: "2026-10-18T09:00:00Z",
  priority: "High",
  completedAtUtc: null,
  createdAtUtc: "2026-10-17T09:00:00Z",
  updatedAtUtc: "2026-10-17T09:00:00Z",
  version: 7,
};
const interview = {
  id: "interview1",
  applicationId: "app1",
  type: "Technical",
  scheduledStartUtc: "2026-10-18T09:00:00Z",
  scheduledEndUtc: "2026-10-18T10:00:00Z",
  location: "Remote",
  meetingUrl: "https://example.com/meeting",
  outcome: null,
  version: 8,
  createdAtUtc: task.createdAtUtc,
  updatedAtUtc: task.updatedAtUtc,
};
const page = (items: unknown[], totalPages = 1) => ({
  items,
  page: 1,
  pageSize: 20,
  totalPages,
  totalCount: items.length,
});
beforeEach(() => {
  vi.resetAllMocks();
  mocks.request.mockImplementation(
    async (path: string, opts?: { method?: string; body?: string }) => {
      if (opts?.method === "PUT")
        return path.includes("/tasks/")
          ? { ...task, version: 9 }
          : { ...interview, version: 9 };
      if (path.includes("/notes")) return page([]);
      if (path.includes("/applications?"))
        return page([
          {
            id: "app1",
            companyName: "Company",
            jobTitle: "Engineer",
            status: "Applied",
          },
        ]);
      if (path.endsWith("/tasks/task1")) return task;
      if (path.endsWith("/interviews/interview1")) return interview;
      if (path.includes("/tasks?")) return page([task], 2);
      if (path.includes("/interviews?")) return page([interview]);
      return {};
    },
  );
});
it("shows real task priority, application link, view filters and pagination", async () => {
  render(<PlanningList tasks />);
  expect(
    await screen.findByRole("link", { name: "Follow up" }),
  ).toHaveAttribute("href", "/tasks/task1");
  expect(screen.getByText("High priority · Open")).toBeVisible();
  expect(
    screen.getByRole("link", { name: "View application" }),
  ).toHaveAttribute("href", "/applications/app1");
  fireEvent.click(screen.getByRole("button", { name: "Next" }));
  await waitFor(() =>
    expect(mocks.request).toHaveBeenCalledWith(
      expect.stringContaining("page=2"),
      expect.anything(),
    ),
  );
  fireEvent.change(screen.getByLabelText("View"), {
    target: { value: "overdue" },
  });
  await waitFor(() =>
    expect(mocks.request).toHaveBeenCalledWith(
      "/api/v1/tasks?view=overdue&page=1",
      expect.anything(),
    ),
  );
});
it("shows an honest empty state and retries list failures", async () => {
  mocks.request.mockRejectedValueOnce(new Error("offline"));
  render(<PlanningList />);
  expect(await screen.findByRole("alert")).toHaveTextContent("try again");
  mocks.request.mockResolvedValueOnce(page([]));
  fireEvent.click(screen.getByRole("button", { name: "Refresh" }));
  expect(await screen.findByText("No interviews in this view.")).toBeVisible();
});
it("edits a task with its saved version and can reopen it", async () => {
  render(<PlanningEditor tasks id="task1" />);
  await screen.findByDisplayValue("Follow up");
  fireEvent.change(screen.getByLabelText("Title"), {
    target: { value: "Prepare examples" },
  });
  fireEvent.click(screen.getByLabelText("Completed"));
  fireEvent.click(screen.getByRole("button", { name: "Save" }));
  await screen.findByText("Saved.");
  const call = mocks.request.mock.calls.find(
    (c: unknown[]) => (c[1] as { method?: string })?.method === "PUT",
  )!;
  expect(JSON.parse(call[1].body)).toMatchObject({
    title: "Prepare examples",
    completed: true,
    version: 7,
    applicationId: "app1",
  });
  fireEvent.click(screen.getByLabelText("Completed"));
  fireEvent.click(screen.getByRole("button", { name: "Save" }));
  await waitFor(() =>
    expect(
      mocks.request.mock.calls.filter(
        (c: unknown[]) => (c[1] as { method?: string })?.method === "PUT",
      ),
    ).toHaveLength(2),
  );
  const body = JSON.parse(mocks.request.mock.calls.at(-1)![1].body);
  expect(body).toMatchObject({ completed: false, version: 9 });
});
it("keeps drafts on conflicts and requires an explicit latest reload", async () => {
  render(<PlanningEditor tasks id="task1" />);
  await screen.findByDisplayValue("Follow up");
  mocks.request.mockRejectedValueOnce(
    new ApiError(409, { title: "Reload before editing again." }),
  );
  fireEvent.change(screen.getByLabelText("Title"), {
    target: { value: "My unsaved draft" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Save" }));
  await screen.findByRole("alert");
  expect(screen.getByLabelText("Title")).toHaveValue("My unsaved draft");
  expect(screen.getByRole("button", { name: "Reload latest" })).toBeVisible();
});
it("rejects an inverted interview time range before sending a mutation", async () => {
  render(<PlanningEditor id="interview1" />);
  await screen.findByDisplayValue("Remote");
  fireEvent.change(screen.getByLabelText("End"), {
    target: { value: "2026-01-01T10:00" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Save" }));
  expect(await screen.findByRole("alert")).toHaveTextContent(
    "end must be after start",
  );
  expect(
    mocks.request.mock.calls.some(
      (c: unknown[]) => (c[1] as { method?: string })?.method === "PUT",
    ),
  ).toBe(false);
});
it("renders private note content as text and preserves its version on edits", async () => {
  const note = {
    id: "n1",
    body: "<script>private</script>",
    createdAtUtc: task.createdAtUtc,
    updatedAtUtc: task.updatedAtUtc,
    version: 4,
  };
  mocks.request.mockResolvedValueOnce(page([note]));
  render(<InterviewNotes id="interview1" />);
  expect(await screen.findByText(note.body)).toBeVisible();
  expect(document.querySelector("script")).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "Edit note" }));
  fireEvent.change(screen.getByLabelText("Edit note"), {
    target: { value: "Updated context" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Save note" }));
  await waitFor(() =>
    expect(mocks.request).toHaveBeenCalledWith(
      "/api/v1/interviews/interview1/notes/n1",
      expect.objectContaining({
        method: "PUT",
        body: JSON.stringify({ body: "Updated context", version: 4 }),
      }),
    ),
  );
});
it("uses UTC entry explicitly and round trips local inputs", () => {
  expect(utcTime("2026-10-18T12:30", "UTC")).toBe("2026-10-18T12:30:00.000Z");
  const local = inputTime("2026-10-18T12:30:00Z", "local");
  expect(utcTime(local, "local")).toBe("2026-10-18T12:30:00.000Z");
  expect(utcTime("", "UTC")).toBeNull();
  expect(() => utcTime("2026-02-30T12:00", "UTC")).toThrow("invalid");
});
it("never deletes when the user cancels the delete dialog", async () => {
  vi.spyOn(window, "confirm").mockReturnValue(false);
  render(<PlanningEditor tasks id="task1" />);
  await screen.findByDisplayValue("Follow up");
  fireEvent.click(screen.getByRole("button", { name: "Delete" }));
  expect(
    mocks.request.mock.calls.some(
      (c: unknown[]) => (c[1] as { method?: string })?.method === "DELETE",
    ),
  ).toBe(false);
});
it("creates an interview with UTC timestamps and only the chosen application", async () => {
  render(<PlanningEditor applicationId="app1" />);
  await screen.findByLabelText("Start");
  fireEvent.change(screen.getByLabelText("Enter times in"), {
    target: { value: "UTC" },
  });
  fireEvent.change(screen.getByLabelText("Start"), {
    target: { value: "2026-10-18T12:00" },
  });
  fireEvent.change(screen.getByLabelText("End"), {
    target: { value: "2026-10-18T13:00" },
  });
  mocks.request.mockResolvedValueOnce(interview);
  fireEvent.click(screen.getByRole("button", { name: "Save" }));
  await waitFor(() =>
    expect(mocks.replace).toHaveBeenCalledWith("/interviews/interview1"),
  );
  const call = mocks.request.mock.calls.find(
    (c: unknown[]) => (c[1] as { method?: string })?.method === "POST",
  )!;
  expect(JSON.parse(call[1].body)).toMatchObject({
    applicationId: "app1",
    scheduledStartUtc: "2026-10-18T12:00:00.000Z",
    scheduledEndUtc: "2026-10-18T13:00:00.000Z",
    type: "Technical",
    version: 0,
  });
});
it("application picker pagination never submits the task form", async () => {
  mocks.request.mockImplementation(async (path: string) =>
    path.includes("/applications?") ? page([], 2) : {},
  );
  render(<PlanningEditor tasks />);
  await screen.findByLabelText("Title");
  fireEvent.change(screen.getByLabelText("Title"), {
    target: { value: "Follow up" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Next" }));
  await waitFor(() =>
    expect(mocks.request).toHaveBeenCalledWith(
      expect.stringContaining("applications?state=all&search=&page=2"),
      expect.anything(),
    ),
  );
  expect(
    mocks.request.mock.calls.some(
      (c: unknown[]) => (c[1] as { method?: string })?.method === "POST",
    ),
  ).toBe(false);
});
