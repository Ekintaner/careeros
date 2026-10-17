import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, expect, it, vi } from "vitest";
import { CompaniesPage, JobsPage } from "./lists";
import { CompanyEditor } from "./company-editor";
import { JobEditor, postingTimestamp } from "./job-editor";
import { JobDetail } from "./job-detail";
import { ApiError } from "@/lib/api-client";
import type { Company, Job } from "./client";
const mocks = vi.hoisted(() => ({ request: vi.fn(), push: vi.fn() }));
vi.mock("@/features/auth/auth-client", () => ({ authClient: () => mocks }));
vi.mock("next/navigation", () => ({ useRouter: () => mocks }));
// Matching has its own read/write/error contract suite; keep opportunity mocks focused.
vi.mock("@/features/matching/matching-panel", () => ({
  MatchingPanel: () => null,
}));
const company: Company = {
  id: "c",
  name: "Example company",
  website: null,
  industry: "Software",
  location: "Riga",
  description: null,
  version: 12,
  createdAtUtc: "2026-01-01T00:00:00Z",
  updatedAtUtc: "2026-01-01T00:00:00Z",
};
const job: Job = {
  id: "j",
  companyId: "c",
  companyName: company.name,
  title: "Engineer",
  description: "Real description",
  location: "Riga",
  workMode: "Remote",
  employmentType: "FullTime",
  seniorityLevel: "Senior",
  source: "Careers",
  sourceUrl: "https://example.com/job",
  salaryMin: 1000,
  salaryMax: 2000,
  salaryCurrency: "EUR",
  salaryPeriod: "Month",
  postedAtUtc: null,
  expiresAtUtc: null,
  archivedAtUtc: null,
  createdAtUtc: company.createdAtUtc,
  updatedAtUtc: company.updatedAtUtc,
  version: 13,
};
const page = (items: unknown[]) => ({
  items,
  page: 1,
  pageSize: 20,
  totalCount: items.length,
  totalPages: items.length ? 1 : 0,
});
it("preserves posting precision during unrelated edits", () => {
  expect(postingTimestamp("2026-01-01T12:34", "2026-01-01T12:34:56.123Z")).toBe(
    "2026-01-01T12:34:56.123Z",
  );
  expect(postingTimestamp("2026-01-02T12:34", "2026-01-01T12:34:56.123Z")).toBe(
    "2026-01-02T12:34:00.000Z",
  );
  expect(postingTimestamp("", "2026-01-01T12:34:56.123Z")).toBeNull();
});
beforeEach(() => {
  vi.resetAllMocks();
  vi.spyOn(window, "confirm").mockReturnValue(true);
  mocks.request.mockImplementation((path: string, init?: RequestInit) => {
    if (init?.method) return Promise.reject(new Error("Unexpected mutation"));
    if (path.startsWith("/api/v1/companies?")) return Promise.resolve(page([]));
    if (path.startsWith("/api/v1/jobs?")) return Promise.resolve(page([]));
    if (path.startsWith("/api/v1/applications?"))
      return Promise.resolve(page([]));
    if (path === "/api/v1/jobs/j") return Promise.resolve(job);
    return Promise.reject(new Error("Unexpected request"));
  });
});
const fill = (name: string, value: string) =>
  fireEvent.change(screen.getByLabelText(name), { target: { value } });
it("shows loading and honest company empty state", async () => {
  render(<CompaniesPage />);
  expect(screen.getByRole("status")).toHaveTextContent("Loading");
  expect(await screen.findByText("No companies yet")).toBeVisible();
  expect(screen.getByRole("link", { name: "Add company" })).toHaveAttribute(
    "href",
    "/companies/new",
  );
});
it("lists companies and keeps records visible after delete conflict", async () => {
  mocks.request.mockResolvedValue(page([{ ...company, jobCount: 1 }]));
  render(<CompaniesPage />);
  expect(await screen.findByRole("link", { name: company.name })).toBeVisible();
  mocks.request.mockRejectedValueOnce(
    new ApiError(409, { title: "Company has jobs. Keep this record." }),
  );
  fireEvent.click(
    screen.getByRole("button", { name: `Delete ${company.name}` }),
  );
  expect(await screen.findByRole("alert")).toHaveTextContent(
    "Company has jobs",
  );
  expect(screen.getByRole("link", { name: company.name })).toBeVisible();
});
it("deletes with the current version and refreshes the list", async () => {
  mocks.request.mockResolvedValueOnce(page([{ ...company, jobCount: 0 }]));
  render(<CompaniesPage />);
  await screen.findByRole("link", { name: company.name });
  mocks.request.mockResolvedValueOnce(undefined).mockResolvedValue(page([]));
  fireEvent.click(
    screen.getByRole("button", { name: `Delete ${company.name}` }),
  );
  expect(await screen.findByText("Company deleted.")).toBeVisible();
  expect(mocks.request).toHaveBeenCalledWith(
    "/api/v1/companies/c?version=12",
    expect.objectContaining({ method: "DELETE" }),
  );
});
it("validates company name and URL before creating", async () => {
  render(<CompanyEditor />);
  fireEvent.click(screen.getByRole("button", { name: "Save company" }));
  expect(screen.getByText("Enter a company name.")).toBeVisible();
  fill("Company name", "Studio");
  fill("Website", "javascript:bad");
  fireEvent.click(screen.getByRole("button", { name: "Save company" }));
  expect(screen.getByText(/Use an HTTP or HTTPS/)).toBeVisible();
  expect(mocks.request).not.toHaveBeenCalled();
  fill("Website", "https://example.com");
  mocks.request.mockResolvedValue(company);
  fireEvent.click(screen.getByRole("button", { name: "Save company" }));
  await waitFor(() =>
    expect(mocks.push).toHaveBeenCalledWith("/companies?saved=1"),
  );
  expect(JSON.parse(mocks.request.mock.calls[0]![1].body)).toMatchObject({
    name: "Studio",
    version: 0,
  });
});
it("edits a company with its version and preserves draft on conflict", async () => {
  render(<CompanyEditor initial={company} />);
  fill("Company name", "Changed");
  mocks.request.mockRejectedValueOnce(
    new ApiError(409, { title: "Reload latest version." }),
  );
  fireEvent.click(screen.getByRole("button", { name: "Save company" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Reload");
  expect(screen.getByLabelText("Company name")).toHaveValue("Changed");
  expect(JSON.parse(mocks.request.mock.calls[0]![1].body).version).toBe(12);
});
it("cancel lets users retain an unsaved company draft", () => {
  render(<CompanyEditor />);
  fill("Company name", "Draft");
  vi.mocked(window.confirm).mockReturnValue(false);
  fireEvent.click(screen.getByRole("button", { name: "Cancel" }));
  expect(mocks.push).not.toHaveBeenCalled();
  expect(screen.getByLabelText("Company name")).toHaveValue("Draft");
});
it("shows jobs empty state", async () => {
  render(<JobsPage />);
  expect(await screen.findByText("No jobs saved yet")).toBeVisible();
});
it("lists jobs and sends search filters and sorting to the server", async () => {
  mocks.request.mockImplementation((path: string) =>
    Promise.resolve(path.includes("/jobs?") ? page([job]) : page([])),
  );
  render(<JobsPage />);
  expect(await screen.findByRole("link", { name: "Engineer" })).toHaveAttribute(
    "href",
    "/jobs/j",
  );
  fireEvent.click(screen.getByText("Filters & sort"));
  fill("Search jobs", "backend");
  fill("Work mode", "Remote");
  fill("Sort by", "title");
  fireEvent.click(screen.getByRole("button", { name: "Search jobs" }));
  await waitFor(() =>
    expect(
      mocks.request.mock.calls.some(
        ([path]) =>
          path.includes("search=backend") &&
          path.includes("workMode=Remote") &&
          path.includes("sort=title") &&
          path.includes("page=1"),
      ),
    ).toBe(true),
  );
});
it("handles list failure with a real retry", async () => {
  mocks.request.mockRejectedValueOnce(new Error("offline"));
  render(<CompaniesPage />);
  expect(await screen.findByRole("alert")).toHaveTextContent("try again");
  fireEvent.click(screen.getByRole("button", { name: "Try again" }));
  expect(await screen.findByText("No companies yet")).toBeVisible();
});
it("requires a job title and an owned company selection", async () => {
  render(<JobEditor />);
  fireEvent.click(screen.getByRole("button", { name: "Save job" }));
  expect(screen.getByText("Enter a job title.")).toBeVisible();
  expect(screen.getByText("Choose one of your companies.")).toBeVisible();
  expect(await screen.findByText(/No companies yet/)).toBeVisible();
  expect(
    screen.getByRole("link", { name: "Create a company" }),
  ).toHaveAttribute("href", "/companies/new");
});
it("creates a job using a company returned by the selector", async () => {
  mocks.request.mockResolvedValue(page([{ ...company, jobCount: 0 }]));
  render(<JobEditor />);
  await screen.findByRole("option", { name: "Example company · Riga" });
  fill("Company (required)", "c");
  fill("Job title", "New role");
  mocks.request.mockResolvedValue(job);
  fireEvent.click(screen.getByRole("button", { name: "Save job" }));
  await waitFor(() =>
    expect(mocks.push).toHaveBeenCalledWith("/jobs/j?saved=1"),
  );
  const mutation = mocks.request.mock.calls.find(
    ([, i]) => i.method === "POST",
  );
  expect(JSON.parse(mutation![1].body)).toMatchObject({
    companyId: "c",
    title: "New role",
    salaryMin: null,
    salaryCurrency: null,
    version: 0,
  });
});
it("validates job salary, URL and dates", () => {
  render(<JobEditor initial={job} />);
  fill("Maximum salary", "999");
  fill("Original posting URL", "file:///private");
  fill("Posted at (UTC)", "2026-05-02T12:00");
  fill("Expires at (UTC)", "2026-05-01T12:00");
  fireEvent.click(screen.getByRole("button", { name: "Save job" }));
  expect(screen.getByText("Maximum cannot be below minimum.")).toBeVisible();
  expect(screen.getByText(/Use an HTTP or HTTPS/)).toBeVisible();
  expect(screen.getByText("Expiration cannot precede posting.")).toBeVisible();
  expect(mocks.request.mock.calls.some(([, i]) => i.method === "PUT")).toBe(
    false,
  );
});
it("edits jobs and prevents a second submission while saving", async () => {
  render(<JobEditor initial={job} />);
  fill("Job title", "Updated role");
  mocks.request.mockReturnValue(new Promise(() => {}));
  fireEvent.click(screen.getByRole("button", { name: "Save job" }));
  expect(screen.getByRole("button", { name: "Saving…" })).toBeDisabled();
  const mutation = mocks.request.mock.calls.find(([, i]) => i.method === "PUT");
  expect(JSON.parse(mutation![1].body)).toMatchObject({
    title: "Updated role",
    version: 13,
  });
});
it("shows real job details and a safe original posting link", async () => {
  render(<JobDetail id="j" />);
  expect(
    await screen.findByRole("heading", { name: "Engineer" }),
  ).toBeVisible();
  expect(screen.getByText("Real description")).toBeVisible();
  const link = screen.getByRole("link", { name: /Open original posting/ });
  expect(link).toHaveAttribute("href", job.sourceUrl);
  expect(link).toHaveAttribute("rel", "noopener noreferrer");
  expect(link).toHaveAttribute("target", "_blank");
  expect(screen.queryByText("Expires")).not.toBeInTheDocument();
});
it("archives jobs with their version and shows the restored action", async () => {
  render(<JobDetail id="j" />);
  await screen.findByRole("heading", { name: "Engineer" });
  mocks.request.mockImplementation((path: string) =>
    Promise.resolve(
      path.startsWith("/api/v1/applications?")
        ? page([])
        : { ...job, archivedAtUtc: job.createdAtUtc },
    ),
  );
  fireEvent.click(screen.getByRole("button", { name: "Archive job" }));
  expect(
    await screen.findByRole("button", { name: "Restore job" }),
  ).toBeVisible();
  expect(mocks.request).toHaveBeenCalledWith(
    "/api/v1/jobs/j/archive",
    expect.objectContaining({
      method: "PUT",
      body: JSON.stringify({ archived: true, version: 13 }),
    }),
  );
});
it("keeps job detail visible when history blocks deletion", async () => {
  render(<JobDetail id="j" />);
  await screen.findByRole("heading", { name: "Engineer" });
  mocks.request.mockRejectedValueOnce(
    new ApiError(409, { title: "Archive jobs with history." }),
  );
  fireEvent.click(screen.getByRole("button", { name: "Delete job" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Archive jobs");
  expect(screen.getByRole("heading", { name: "Engineer" })).toBeVisible();
  expect(mocks.push).not.toHaveBeenCalled();
});
