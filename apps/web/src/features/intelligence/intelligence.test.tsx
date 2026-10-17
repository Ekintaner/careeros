import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { beforeEach, expect, it, vi } from "vitest";
import { ApiError } from "@/lib/api-client";
import { SkillsIntelligencePage } from "./intelligence-page";
import type { Intelligence, SkillJobs } from "./types";
const mocks = vi.hoisted(() => ({ request: vi.fn() }));
vi.mock("@/features/auth/auth-client", () => ({ authClient: () => mocks }));
const base: Intelligence = {
  algorithm: "skills-intelligence-v1 / skills-v1",
  state: "active",
  historyAvailable: false,
  summary: {
    totalJobs: 3,
    analyzedJobs: 2,
    unassessedJobs: 1,
    distinctSkills: 4,
    profileSkills: 2,
    supportedSkills: 1,
    evidenceGapSkills: 2,
    missingSkills: 1,
    earnedProfilePoints: 5,
    possiblePoints: 12,
    profileCoverage: 41.7,
  },
  items: [
    {
      skillId: "docker",
      name: "Docker",
      jobCount: 2,
      requiredJobs: 2,
      preferredJobs: 0,
      inProfile: false,
      confirmedResumes: 0,
      status: "missing",
      action: "Learn or assess",
      weightedDemand: 6,
      demandPercent: 100,
      actionPriority: 100,
      learningPriority: 100,
    },
    {
      skillId: "react",
      name: "React",
      jobCount: 1,
      requiredJobs: 0,
      preferredJobs: 1,
      inProfile: true,
      confirmedResumes: 0,
      status: "evidenceGap",
      action: "Document resume evidence",
      weightedDemand: 1,
      demandPercent: 50,
      actionPriority: 16.7,
      learningPriority: 0,
    },
    {
      skillId: "go",
      name: "Go",
      jobCount: 1,
      requiredJobs: 0,
      preferredJobs: 1,
      inProfile: false,
      confirmedResumes: 1,
      status: "evidenceGap",
      action: "Review profile record",
      weightedDemand: 1,
      demandPercent: 50,
      actionPriority: 16.7,
      learningPriority: 0,
    },
    {
      skillId: "sql",
      name: "SQL",
      jobCount: 2,
      requiredJobs: 1,
      preferredJobs: 1,
      inProfile: true,
      confirmedResumes: 1,
      status: "supported",
      action: "Maintain",
      weightedDemand: 4,
      demandPercent: 100,
      actionPriority: 0,
      learningPriority: 0,
    },
  ],
  page: 1,
  pageSize: 20,
  totalCount: 4,
  totalPages: 1,
};
const jobs: SkillJobs = {
  skillId: "docker",
  name: "Docker",
  items: [
    {
      id: "j",
      title: "Backend engineer",
      companyName: "Studio",
      requirementLevel: "Required",
      archived: false,
      earnedProfilePoints: 4,
      possiblePoints: 8,
      profileCoverage: 50,
    },
  ],
  page: 1,
  pageSize: 10,
  totalCount: 1,
  totalPages: 1,
};
beforeEach(() => {
  vi.resetAllMocks();
  mocks.request.mockImplementation((path: string) =>
    Promise.resolve(path.includes("/jobs?") ? jobs : base),
  );
});
it("shows real weighted coverage, missing/evidence distinctions and no invented history", async () => {
  render(<SkillsIntelligencePage />);
  expect(screen.getByRole("status")).toHaveTextContent("Analyzing");
  expect(await screen.findByText("41.7%")).toBeVisible();
  expect(screen.getByText("5/12 weighted points")).toBeVisible();
  expect(screen.getByText("1 without recorded requirements")).toBeVisible();
  expect(screen.getByText("Learn or assess")).toBeVisible();
  expect(screen.getByText("Document resume evidence")).toBeVisible();
  expect(screen.getByText("Review profile record")).toBeVisible();
  expect(screen.getByText("Maintain")).toBeVisible();
  expect(screen.getByText(/Trends are not available yet/)).toBeVisible();
  expect(
    screen.getByRole("meter", { name: "Demand share for Docker" }),
  ).toHaveAttribute("value", "100");
  expect(
    screen.getByRole("link", { name: "Update career profile" }),
  ).toHaveAttribute("href", "/profile");
});
it("separates profile-only and resume-only evidence from learning gaps", async () => {
  render(<SkillsIntelligencePage />);
  await screen.findByText("41.7%");
  const react = screen
    .getByRole("heading", { name: "React" })
    .closest("article")!;
  expect(within(react).getByText("✓ Recorded in career profile")).toBeVisible();
  expect(within(react).getByText("No confirmed resume evidence")).toBeVisible();
  expect(within(react).getByText("Evidence review priority")).toBeVisible();
  const go = screen.getByRole("heading", { name: "Go" }).closest("article")!;
  expect(within(go).getByText("Not recorded in career profile")).toBeVisible();
  expect(within(go).getByText("Confirmed in 1 resume")).toBeVisible();
  expect(
    within(go).queryByText("Learning / assessment priority"),
  ).not.toBeInTheDocument();
});
it("explains priorities and aggregate coverage without presenting percentages as predictions", async () => {
  render(<SkillsIntelligencePage />);
  await screen.findByText("41.7%");
  fireEvent.click(screen.getByText("How priorities and evidence work"));
  expect(screen.getByText(/Action priority =/)).toHaveTextContent(
    "3 × jobs with requirements",
  );
  expect(
    screen.getByText(/Profile coverage is total earned/),
  ).toHaveTextContent("not the average");
});
it("empty collection is honest and does not turn unknown coverage into 0%", async () => {
  mocks.request.mockResolvedValue({
    ...base,
    summary: {
      ...base.summary,
      totalJobs: 0,
      analyzedJobs: 0,
      unassessedJobs: 0,
      distinctSkills: 0,
      profileSkills: 0,
      supportedSkills: 0,
      evidenceGapSkills: 0,
      missingSkills: 0,
      earnedProfilePoints: 0,
      possiblePoints: 0,
      profileCoverage: null,
    },
    items: [],
    totalCount: 0,
    totalPages: 0,
  });
  render(<SkillsIntelligencePage />);
  expect(
    await screen.findByRole("heading", { name: "No saved jobs in this scope" }),
  ).toBeVisible();
  expect(screen.getByText("Not scored")).toBeVisible();
  expect(screen.queryByText("0%")).not.toBeInTheDocument();
});
it("jobs without structured requirements direct users to matching setup", async () => {
  mocks.request.mockResolvedValue({
    ...base,
    summary: {
      ...base.summary,
      totalJobs: 2,
      analyzedJobs: 0,
      unassessedJobs: 2,
      profileCoverage: null,
    },
    items: [],
  });
  render(<SkillsIntelligencePage />);
  expect(
    await screen.findByRole("heading", {
      name: "Add job requirements to begin",
    }),
  ).toBeVisible();
  expect(
    screen.getByRole("link", { name: "Review saved jobs" }),
  ).toHaveAttribute("href", "/jobs");
});
it("requests scope/status/sort/search on the server and retains scope when clearing skill filters", async () => {
  render(<SkillsIntelligencePage />);
  await screen.findByText("41.7%");
  fireEvent.change(screen.getByLabelText("Job scope"), {
    target: { value: "archived" },
  });
  await screen.findByText("41.7%");
  fireEvent.change(screen.getByLabelText("Evidence status"), {
    target: { value: "missing" },
  });
  await screen.findByText("41.7%");
  fireEvent.change(screen.getByLabelText("Sort skills"), {
    target: { value: "demand" },
  });
  await screen.findByText("41.7%");
  fireEvent.change(screen.getByLabelText("Search requested skills"), {
    target: { value: "C++" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Search skills" }));
  await waitFor(() =>
    expect(mocks.request).toHaveBeenCalledWith(
      expect.stringContaining(
        "state=archived&status=missing&sort=demand&page=1&pageSize=20&search=C%2B%2B",
      ),
      expect.anything(),
    ),
  );
  await screen.findByText("41.7%");
  fireEvent.click(screen.getByRole("button", { name: "Clear skill filters" }));
  expect(screen.getByLabelText("Job scope")).toHaveValue("archived");
});
it("lazy-loads the jobs demanding a skill with current complete-job profile matching", async () => {
  render(<SkillsIntelligencePage />);
  const button = await screen.findByRole("button", {
    name: "View jobs for Docker (2)",
  });
  expect(
    mocks.request.mock.calls.some(([path]) => path.includes("/jobs?")),
  ).toBe(false);
  fireEvent.click(button);
  expect(
    await screen.findByRole("link", { name: "Backend engineer" }),
  ).toHaveAttribute("href", "/jobs/j");
  expect(screen.getByText("50% profile coverage")).toBeVisible();
  expect(screen.getByText("4/8 points")).toBeVisible();
  expect(button).toHaveAttribute("aria-expanded", "true");
  fireEvent.click(button);
  expect(button).toHaveAttribute("aria-expanded", "false");
});
it("paginates both skill results and skill job details", async () => {
  mocks.request.mockImplementation((path: string) =>
    Promise.resolve(
      path.includes("/jobs?")
        ? { ...jobs, totalPages: 2, totalCount: 11 }
        : { ...base, totalPages: 2, totalCount: 21 },
    ),
  );
  render(<SkillsIntelligencePage />);
  fireEvent.click(await screen.findByRole("button", { name: "Next skills" }));
  await waitFor(() =>
    expect(mocks.request).toHaveBeenCalledWith(
      expect.stringContaining("page=2&pageSize=20"),
      expect.anything(),
    ),
  );
  fireEvent.click(
    await screen.findByRole("button", { name: "View jobs for Docker (2)" }),
  );
  fireEvent.click(await screen.findByRole("button", { name: "Next jobs" }));
  await waitFor(() =>
    expect(mocks.request).toHaveBeenCalledWith(
      expect.stringContaining("docker/jobs?state=active&page=2&pageSize=10"),
      expect.anything(),
    ),
  );
});
it("handles filtered empty results without inventing rows", async () => {
  mocks.request.mockResolvedValue({
    ...base,
    items: [],
    totalCount: 0,
    totalPages: 0,
  });
  render(<SkillsIntelligencePage />);
  expect(
    await screen.findByRole("heading", {
      name: "No skills match these filters",
    }),
  ).toBeVisible();
  expect(
    screen.queryByRole("heading", { name: "Docker" }),
  ).not.toBeInTheDocument();
  expect(
    screen.getByRole("button", { name: "Show all requested skills" }),
  ).toBeVisible();
});
it("recovers from an analysis error without showing fake summary values", async () => {
  mocks.request.mockRejectedValue(new Error("offline"));
  render(<SkillsIntelligencePage />);
  expect(await screen.findByRole("alert")).toBeVisible();
  expect(screen.queryByText("41.7%")).not.toBeInTheDocument();
  mocks.request.mockResolvedValue(base);
  fireEvent.click(screen.getByRole("button", { name: "Retry analysis" }));
  expect(await screen.findByText("41.7%")).toBeVisible();
});
it("retains the analysis if an individual skill drilldown fails and offers retry", async () => {
  render(<SkillsIntelligencePage />);
  await screen.findByText("41.7%");
  mocks.request.mockRejectedValueOnce(
    new ApiError(404, { title: "No longer available" }),
  );
  fireEvent.click(
    screen.getByRole("button", { name: "View jobs for Docker (2)" }),
  );
  expect(await screen.findByRole("alert")).toBeVisible();
  expect(screen.getByText("41.7%")).toBeVisible();
  fireEvent.click(screen.getByRole("button", { name: "Retry jobs" }));
  expect(
    await screen.findByRole("link", { name: "Backend engineer" }),
  ).toBeVisible();
});
it("ignores an obsolete response after changing scopes", async () => {
  let resolveOld!: (data: Intelligence) => void;
  mocks.request.mockImplementationOnce(
    () =>
      new Promise<Intelligence>((resolve) => {
        resolveOld = resolve;
      }),
  );
  mocks.request.mockResolvedValue({
    ...base,
    summary: { ...base.summary, profileCoverage: 25 },
  });
  render(<SkillsIntelligencePage />);
  fireEvent.change(screen.getByLabelText("Job scope"), {
    target: { value: "archived" },
  });
  expect(await screen.findByText("25%")).toBeVisible();
  await act(async () => {
    resolveOld(base);
  });
  expect(screen.queryByText("41.7%")).not.toBeInTheDocument();
  expect(screen.getByText("25%")).toBeVisible();
});
