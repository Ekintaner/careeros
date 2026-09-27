import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { beforeEach, expect, it, vi } from "vitest";
import { ApplicationsPage } from "./pipeline";
import { ApplicationEditor } from "./editor";
import { ApplicationDetail } from "./detail";
import { StatusControl } from "./status-control";
import { JobApplicationLink } from "./job-link";
import { ApiError } from "@/lib/api-client";
import { statuses, type ApplicationView } from "./client";
const mocks = vi.hoisted(() => ({ request: vi.fn(), push: vi.fn() }));
vi.mock("@/features/auth/auth-client", () => ({ authClient: () => mocks }));
vi.mock("next/navigation", () => ({ useRouter: () => mocks }));
const a: ApplicationView = {
  id: "a",
  jobId: "j",
  companyId: "c",
  jobTitle: "Engineer",
  companyName: "Example",
  location: "Riga",
  status: "Applied",
  resumeId: null,
  resumeName: null,
  source: "Referral",
  sourceUrl: "https://example.com/job",
  appliedAtUtc: "2026-01-01T12:34:56Z",
  createdAtUtc: "2026-01-01T12:34:56Z",
  updatedAtUtc: "2026-01-01T12:34:56Z",
  lastStatusChangedAtUtc: "2026-01-01T12:34:56Z",
  version: 7,
};
const job = { id: "j", title: "Engineer", companyName: "Example" };
const page = (items: unknown[]) => ({
  items,
  page: 1,
  pageSize: 20,
  totalCount: items.length,
  totalPages: items.length ? 1 : 0,
});
function reads(path: string, init?: RequestInit): Promise<unknown> {
  if (init?.method) return Promise.reject(new Error("Unexpected mutation"));
  if (path.startsWith("/api/v1/applications/summary"))
    return Promise.resolve(statuses.map((status) => ({ status, count: 0 })));
  if (path === "/api/v1/applications/a") return Promise.resolve(a);
  if (path.includes("/history?"))
    return Promise.resolve(
      page([
        {
          id: "h",
          fromStatus: null,
          toStatus: "Applied",
          sequence: 0,
          changedAtUtc: a.createdAtUtc,
        },
      ]),
    );
  if (path === "/api/v1/jobs/j") return Promise.resolve(job);
  if (path.startsWith("/api/v1/jobs?")) return Promise.resolve(page([job]));
  return Promise.resolve(page([]));
}
const fill = (label: string, value: string) =>
  fireEvent.change(screen.getByLabelText(label), { target: { value } });
beforeEach(() => {
  vi.resetAllMocks();
  vi.spyOn(window, "confirm").mockReturnValue(true);
  mocks.request.mockImplementation(reads);
});
it("shows an honest empty pipeline and real zero counts", async () => {
  render(<ApplicationsPage />);
  expect(await screen.findByText(/No active applications match/)).toBeVisible();
  expect(screen.getByLabelText("Offer count")).toHaveTextContent("0");
  expect(
    screen.getByRole("link", { name: "Track application" }),
  ).toHaveAttribute("href", "/applications/new");
});
it("renders server counts independently of the bounded cards and a mobile stage selector", async () => {
  mocks.request.mockImplementation((path, init) =>
    path.includes("/summary")
      ? Promise.resolve(
          statuses.map((status) => ({
            status,
            count: status === "Applied" ? 31 : 0,
          })),
        )
      : path.includes("status=Applied")
        ? Promise.resolve(page([a]))
        : reads(path, init),
  );
  render(<ApplicationsPage />);
  expect(await screen.findByRole("link", { name: "Engineer" })).toHaveAttribute(
    "href",
    "/applications/a",
  );
  expect(screen.getByLabelText("Applied count")).toHaveTextContent("31");
  fill("Pipeline stage", "Interview");
  expect(screen.getByLabelText("Pipeline stage")).toHaveValue("Interview");
});
it("submits filters to the server and separates archived records", async () => {
  render(<ApplicationsPage />);
  await screen.findByText(/No active applications match/);
  fireEvent.click(screen.getByText("Search & filters"));
  fill("Search jobs or companies", "Engineer");
  fireEvent.click(screen.getByRole("button", { name: "Apply filters" }));
  await waitFor(() =>
    expect(mocks.request).toHaveBeenCalledWith(
      expect.stringContaining("search=Engineer"),
      expect.anything(),
    ),
  );
  fireEvent.click(screen.getByRole("button", { name: "Archived" }));
  await waitFor(() =>
    expect(mocks.request).toHaveBeenCalledWith(
      expect.stringContaining("state=archived"),
      expect.anything(),
    ),
  );
});
it("moves through an explicit accessible control only after the server succeeds", async () => {
  const changed = vi.fn();
  mocks.request.mockResolvedValue(a);
  render(<StatusControl application={a} onChanged={changed} />);
  expect(screen.getByRole("button", { name: "Move" })).toBeDisabled();
  fill("Move Engineer to", "Interview");
  fireEvent.click(screen.getByRole("button", { name: "Move" }));
  await waitFor(() => expect(changed).toHaveBeenCalledOnce());
  expect(mocks.request).toHaveBeenCalledWith(
    "/api/v1/applications/a/status",
    expect.objectContaining({
      method: "POST",
      body: JSON.stringify({ status: "Interview", version: 7 }),
    }),
  );
});
it("preserves the original card on failed movement", async () => {
  const changed = vi.fn();
  mocks.request.mockRejectedValue(new Error("Offline"));
  render(<StatusControl application={a} onChanged={changed} />);
  fill("Move Engineer to", "Offer");
  fireEvent.click(screen.getByRole("button", { name: "Move" }));
  expect(await screen.findByRole("alert")).toHaveTextContent(
    "couldn’t complete",
  );
  expect(changed).not.toHaveBeenCalled();
  expect(screen.getByLabelText("Move Engineer to")).toHaveValue("Offer");
});
it("requires refreshing a stale stage instead of retrying an overwrite", async () => {
  const changed = vi.fn();
  mocks.request.mockRejectedValue(
    new ApiError(409, { title: "This record changed." }),
  );
  render(<StatusControl application={a} onChanged={changed} />);
  fill("Move Engineer to", "Offer");
  fireEvent.click(screen.getByRole("button", { name: "Move" }));
  await screen.findByRole("alert");
  expect(screen.getByRole("button", { name: "Move" })).toBeDisabled();
  fireEvent.click(screen.getByRole("button", { name: "Reload current stage" }));
  expect(changed).toHaveBeenCalledOnce();
});
it("creates directly as Applied from an existing job and explains absent resumes", async () => {
  mocks.request.mockImplementation((path, init) =>
    init?.method === "POST" ? Promise.resolve(a) : reads(path, init),
  );
  render(<ApplicationEditor jobId="j" />);
  await screen.findByText("Engineer");
  await screen.findByText("No resumes available yet.");
  fill("Initial status", "Applied");
  fireEvent.click(screen.getByRole("button", { name: "Create application" }));
  await waitFor(() =>
    expect(mocks.push).toHaveBeenCalledWith("/applications/a"),
  );
  expect(mocks.request).toHaveBeenCalledWith(
    "/api/v1/applications",
    expect.objectContaining({
      method: "POST",
      body: expect.stringContaining('"initialStatus":"Applied"'),
    }),
  );
});
it("requires choosing an existing job for manual creation", async () => {
  render(<ApplicationEditor />);
  await screen.findByRole("option", { name: "Engineer · Example" });
  expect(screen.getByLabelText("Job")).toBeRequired();
  fill("Job", "j");
  expect(screen.getByLabelText("Job")).toHaveValue("j");
});
it("offers the existing application after a duplicate conflict", async () => {
  mocks.request.mockImplementation((path, init) =>
    init?.method
      ? Promise.reject(
          new ApiError(409, { title: "This job already has an application." }),
        )
      : path.includes("jobId=j")
        ? Promise.resolve(page([a]))
        : reads(path, init),
  );
  render(<ApplicationEditor jobId="j" />);
  await screen.findByText("Engineer");
  fireEvent.click(screen.getByRole("button", { name: "Create application" }));
  expect(
    await screen.findByRole("link", { name: "View existing application" }),
  ).toHaveAttribute("href", "/applications/a");
  expect(mocks.push).not.toHaveBeenCalled();
});
it("preserves the exact applied timestamp on unrelated metadata edits", async () => {
  mocks.request.mockImplementation((path, init) =>
    init?.method === "PUT" ? Promise.resolve(a) : reads(path, init),
  );
  render(<ApplicationEditor id="a" />);
  await screen.findByText("Engineer");
  fill("Application source (optional)", "Company site");
  fireEvent.click(screen.getByRole("button", { name: "Save application" }));
  await waitFor(() => expect(mocks.push).toHaveBeenCalled());
  expect(mocks.request).toHaveBeenCalledWith(
    "/api/v1/applications/a",
    expect.objectContaining({
      body: expect.stringContaining('"appliedAtUtc":"2026-01-01T12:34:56Z"'),
    }),
  );
});
it("shows actual detail and an immutable initial history event", async () => {
  render(<ApplicationDetail id="a" />);
  expect(
    await screen.findByRole("heading", { name: "Engineer" }),
  ).toBeVisible();
  expect(await screen.findByText("Initial status")).toBeVisible();
  expect(screen.getByText("Referral")).toBeVisible();
  expect(
    screen.getByRole("link", { name: "Original posting ↗" }),
  ).toHaveAttribute("rel", "noopener noreferrer");
});
it("adds a note without interpreting HTML", async () => {
  let saved = false;
  mocks.request.mockImplementation((path, init) => {
    if (path.includes("/notes") && init?.method === "POST") {
      saved = true;
      return Promise.resolve({});
    }
    if (path.includes("/notes?") && saved)
      return Promise.resolve(
        page([
          {
            id: "n",
            body: "<script>private</script>",
            createdAtUtc: a.createdAtUtc,
            updatedAtUtc: a.createdAtUtc,
            version: 8,
          },
        ]),
      );
    return reads(path, init);
  });
  render(<ApplicationDetail id="a" />);
  await screen.findByLabelText("New note");
  fill("New note", "<script>private</script>");
  fireEvent.click(screen.getByRole("button", { name: "Add note" }));
  expect(await screen.findByText("<script>private</script>")).toBeVisible();
  expect(document.querySelector("script")).toBeNull();
  expect(screen.getByLabelText("New note")).toHaveValue("");
});
it("edits and deletes notes using their current versions", async () => {
  let note: unknown[] = [
    {
      id: "n",
      body: "Original note",
      createdAtUtc: a.createdAtUtc,
      updatedAtUtc: a.createdAtUtc,
      version: 8,
    },
  ];
  mocks.request.mockImplementation((path, init) => {
    if (path.includes("/notes/n") && init?.method) {
      note =
        init.method === "DELETE"
          ? []
          : [
              {
                id: "n",
                body: "Updated note",
                createdAtUtc: a.createdAtUtc,
                updatedAtUtc: a.createdAtUtc,
                version: 9,
              },
            ];
      return Promise.resolve({});
    }
    if (path.includes("/notes?")) return Promise.resolve(page(note));
    return reads(path, init);
  });
  render(<ApplicationDetail id="a" />);
  await screen.findByText("Original note");
  fireEvent.click(screen.getByRole("button", { name: "Edit note" }));
  fill("Edit note", "Updated note");
  fireEvent.click(screen.getByRole("button", { name: "Save note" }));
  await screen.findByText("Updated note");
  fireEvent.click(await screen.findByRole("button", { name: "Delete note" }));
  await screen.findByText("No notes yet.");
  expect(mocks.request).toHaveBeenCalledWith(
    "/api/v1/applications/a/notes/n?version=9",
    expect.objectContaining({ method: "DELETE" }),
  );
});
it("archives and restores with server versions", async () => {
  let current = a;
  mocks.request.mockImplementation((path, init) => {
    if (path.endsWith("/archive") && init?.method) {
      current = { ...a, status: "Archived", version: 8 };
      return Promise.resolve(current);
    }
    if (path.endsWith("/restore") && init?.method) {
      current = { ...a, version: 9 };
      return Promise.resolve(current);
    }
    if (path === "/api/v1/applications/a") return Promise.resolve(current);
    return reads(path, init);
  });
  render(<ApplicationDetail id="a" />);
  fireEvent.click(
    await screen.findByRole("button", { name: "Archive application" }),
  );
  fireEvent.click(
    await screen.findByRole("button", { name: "Restore application" }),
  );
  await screen.findByRole("button", { name: "Archive application" });
  expect(mocks.request).toHaveBeenCalledWith(
    "/api/v1/applications/a/restore",
    expect.objectContaining({ body: '{"version":8}' }),
  );
});
it("links a job to creation or its existing application", async () => {
  const { unmount } = render(<JobApplicationLink jobId="j" />);
  expect(
    await screen.findByRole("link", { name: "Track application" }),
  ).toHaveAttribute("href", "/applications/new?jobId=j");
  unmount();
  mocks.request.mockResolvedValue(page([a]));
  render(<JobApplicationLink jobId="j" />);
  expect(
    await screen.findByRole("link", { name: "View application" }),
  ).toHaveAttribute("href", "/applications/a");
});
it("shows a safe failure without inventing application data", async () => {
  mocks.request.mockRejectedValue(new ApiError(404, {}));
  render(<ApplicationDetail id="missing" />);
  expect(await screen.findByRole("alert")).toHaveTextContent(
    "no longer available",
  );
  expect(screen.queryByRole("heading", { name: "Engineer" })).toBeNull();
  expect(
    within(document.body).getByRole("button", {
      name: "Reload latest application",
    }),
  ).toBeEnabled();
});

it("keeps a note draft when the application status reloads", async () => {
  mocks.request.mockImplementation((path, init) =>
    init?.method === "POST"
      ? Promise.resolve({ ...a, status: "Interview", version: 8 })
      : reads(path, init),
  );
  render(<ApplicationDetail id="a" />);
  await screen.findByLabelText("New note");
  fill("New note", "Unfinished private context");
  fill("Move Engineer to", "Interview");
  fireEvent.click(screen.getByRole("button", { name: "Move" }));
  await waitFor(() =>
    expect(
      mocks.request.mock.calls.filter(
        ([path]) => path === "/api/v1/applications/a",
      ).length,
    ).toBe(2),
  );
  expect(screen.getByLabelText("New note")).toHaveValue(
    "Unfinished private context",
  );
});

it("preserves a conflicting note draft and requires reload", async () => {
  mocks.request.mockImplementation((path, init) =>
    init?.method
      ? Promise.reject(new ApiError(409, { title: "Note changed. Reload." }))
      : reads(path, init),
  );
  render(<ApplicationDetail id="a" />);
  await screen.findByLabelText("New note");
  fill("New note", "Keep this draft");
  fireEvent.click(screen.getByRole("button", { name: "Add note" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Note changed");
  expect(screen.getByLabelText("New note")).toHaveValue("Keep this draft");
  expect(screen.getByRole("button", { name: "Add note" })).toBeDisabled();
  expect(screen.getByRole("button", { name: "Reload notes" })).toBeEnabled();
});
