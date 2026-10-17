import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { beforeEach, expect, it, vi } from "vitest";
import { ApiError } from "@/lib/api-client";
import { MatchingPanel } from "./matching-panel";
import type { Matching } from "./types";
const mocks = vi.hoisted(() => ({ request: vi.fn() }));
vi.mock("@/features/auth/auth-client", () => ({ authClient: () => mocks }));
const timestamp = "2026-10-17T00:00:00Z";
const base: Matching = {
  jobId: "j",
  jobVersion: 10,
  source: "Profile",
  resume: null,
  result: {
    algorithm: "skills-v1",
    score: 75,
    required: { matched: 1, total: 1, earnedPoints: 3, possiblePoints: 3 },
    preferred: { matched: 0, total: 1, earnedPoints: 0, possiblePoints: 1 },
    skills: [
      {
        skillId: "c",
        name: "C++",
        requirementLevel: "Required",
        weight: 3,
        earnedPoints: 3,
        matched: true,
        evidenceRecordedAtUtc: timestamp,
      },
      {
        skillId: "s",
        name: "SQL",
        requirementLevel: "Preferred",
        weight: 1,
        earnedPoints: 0,
        matched: false,
        evidenceRecordedAtUtc: null,
      },
    ],
  },
};
const resume: Matching = {
  ...base,
  source: "Resume",
  resume: { id: "r", name: "My CV", version: 20, skills: [] },
  result: {
    ...base.result,
    score: 0,
    required: { ...base.result.required, matched: 0, earnedPoints: 0 },
    skills: base.result.skills.map((s) => ({
      ...s,
      matched: false,
      earnedPoints: 0,
      evidenceRecordedAtUtc: null,
    })),
  },
};
beforeEach(() => {
  vi.resetAllMocks();
  vi.spyOn(window, "confirm").mockReturnValue(true);
  mocks.request.mockImplementation((path: string, init?: RequestInit) => {
    if (init?.method) return Promise.resolve(undefined);
    if (path.includes("documents?"))
      return Promise.resolve({
        items: [{ resumeId: "r", name: "My CV" }],
        totalCount: 1,
      });
    return Promise.resolve(path.includes("resumeId=") ? resume : base);
  });
});
it("explains score, separate groups, points and dated evidence", async () => {
  render(<MatchingPanel jobId="j" />);
  expect(screen.getByText("Loading skill match…")).toBeVisible();
  expect(await screen.findByText("75% recorded skill coverage")).toBeVisible();
  expect(screen.getByText("1/1 matched · 3/3 points")).toBeVisible();
  expect(screen.getByRole("heading", { name: "Matched skills" })).toBeVisible();
  expect(
    screen.getByRole("heading", { name: "Missing evidence" }),
  ).toBeVisible();
  expect(screen.getByText(/Profile self-assessment updated/)).toBeVisible();
  expect(screen.getByText(/No profile skill evidence recorded/)).toBeVisible();
  expect(
    screen.getByRole("link", { name: "Edit career profile skills" }),
  ).toHaveAttribute("href", "/profile");
});
it("uses only selected resume evidence and never silently copies profile", async () => {
  render(<MatchingPanel jobId="j" />);
  await screen.findByRole("option", { name: "My CV" });
  fireEvent.change(screen.getByLabelText("Compare against"), {
    target: { value: "r" },
  });
  expect(await screen.findByText("0% recorded skill coverage")).toBeVisible();
  expect(
    screen.getByText(/No skills confirmed for this resume yet/),
  ).toBeVisible();
  expect(mocks.request).toHaveBeenCalledWith(
    "/api/v1/jobs/j/matching?resumeId=r",
    expect.anything(),
  );
  expect(
    screen.queryByText(/Profile self-assessment updated/),
  ).not.toBeInTheDocument();
});
it("does not present no-requirement input as zero or perfect match", async () => {
  mocks.request.mockImplementation((path: string) =>
    Promise.resolve(
      path.includes("documents?")
        ? { items: [], totalCount: 0 }
        : {
            ...base,
            result: {
              ...base.result,
              score: null,
              skills: [],
              required: {
                matched: 0,
                total: 0,
                earnedPoints: 0,
                possiblePoints: 0,
              },
              preferred: {
                matched: 0,
                total: 0,
                earnedPoints: 0,
                possiblePoints: 0,
              },
            },
          },
    ),
  );
  render(<MatchingPanel jobId="j" />);
  expect(
    await screen.findByText("Not scored — add job requirements first"),
  ).toBeVisible();
  expect(
    screen.queryByText(/% recorded skill coverage/),
  ).not.toBeInTheDocument();
});
it("saves explicit required/preferred skill names with concurrency version", async () => {
  render(<MatchingPanel jobId="j" />);
  fireEvent.click(
    await screen.findByRole("button", { name: "Edit job requirements" }),
  );
  fireEvent.change(screen.getByLabelText("Required skills"), {
    target: { value: "C++\n.NET" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Save skills" }));
  await waitFor(() =>
    expect(mocks.request).toHaveBeenCalledWith(
      "/api/v1/jobs/j/skills",
      expect.objectContaining({
        method: "PUT",
        body: JSON.stringify({
          version: 10,
          skills: [
            { name: "C++", requirementLevel: "Required" },
            { name: ".NET", requirementLevel: "Required" },
            { name: "SQL", requirementLevel: "Preferred" },
          ],
        }),
      }),
    ),
  );
  expect(
    await screen.findByText("Skills saved. Match recalculated."),
  ).toBeVisible();
  expect(
    mocks.request.mock.calls.some(([path]) => path.includes("profile/skills")),
  ).toBe(false);
});
it("rejects duplicate names across groups without writing", async () => {
  render(<MatchingPanel jobId="j" />);
  fireEvent.click(
    await screen.findByRole("button", { name: "Edit job requirements" }),
  );
  fireEvent.change(screen.getByLabelText("Preferred skills"), {
    target: { value: " c++ " },
  });
  fireEvent.click(screen.getByRole("button", { name: "Save skills" }));
  expect(await screen.findByRole("alert")).toHaveTextContent(
    "Remove duplicate skills",
  );
  expect(
    mocks.request.mock.calls.some(([, init]) => init?.method === "PUT"),
  ).toBe(false);
});
it("requires an explicit PDF evidence confirmation before saving resume skills", async () => {
  render(<MatchingPanel jobId="j" />);
  await screen.findByRole("option", { name: "My CV" });
  fireEvent.change(screen.getByLabelText("Compare against"), {
    target: { value: "r" },
  });
  fireEvent.click(
    await screen.findByRole("button", { name: "Confirm resume skills" }),
  );
  fireEvent.change(screen.getByLabelText("Skills evidenced in this resume"), {
    target: { value: "SQL" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Save skills" }));
  expect(await screen.findByRole("alert")).toHaveTextContent(
    "Confirm that every listed skill",
  );
  fireEvent.click(screen.getByRole("checkbox"));
  fireEvent.click(screen.getByRole("button", { name: "Save skills" }));
  await waitFor(() =>
    expect(mocks.request).toHaveBeenCalledWith(
      "/api/v1/resumes/r/skills",
      expect.objectContaining({
        method: "PUT",
        body: JSON.stringify({ version: 20, names: ["SQL"] }),
      }),
    ),
  );
});
it("keeps conflicting drafts until explicit discard and reload", async () => {
  render(<MatchingPanel jobId="j" />);
  fireEvent.click(
    await screen.findByRole("button", { name: "Edit job requirements" }),
  );
  fireEvent.change(screen.getByLabelText("Required skills"), {
    target: { value: "Rust" },
  });
  mocks.request.mockRejectedValueOnce(
    new ApiError(409, { title: "This record changed." }),
  );
  fireEvent.click(screen.getByRole("button", { name: "Save skills" }));
  expect(await screen.findByRole("alert")).toHaveTextContent(
    "This record changed",
  );
  expect(screen.getByLabelText("Required skills")).toHaveValue("Rust");
  expect(screen.getByRole("button", { name: "Save skills" })).toBeDisabled();
  fireEvent.click(
    screen.getByRole("button", { name: "Discard and reload latest skills" }),
  );
  expect(
    await screen.findByRole("button", { name: "Edit job requirements" }),
  ).toBeVisible();
});
it("has retry for failed reads and can recover", async () => {
  mocks.request.mockRejectedValue(new Error("offline"));
  render(<MatchingPanel jobId="j" />);
  expect((await screen.findAllByRole("alert")).length).toBeGreaterThan(0);
  mocks.request.mockImplementation((path: string) =>
    Promise.resolve(
      path.includes("documents?") ? { items: [], totalCount: 0 } : base,
    ),
  );
  fireEvent.click(screen.getByRole("button", { name: "Reload skill match" }));
  expect(await screen.findByText("75% recorded skill coverage")).toBeVisible();
});
it("paginates resume choices so older resumes remain reachable", async () => {
  mocks.request.mockImplementation((path: string) =>
    Promise.resolve(
      path.includes("documents?")
        ? {
            items: [
              {
                resumeId: path.includes("page=2") ? "old" : "r",
                name: path.includes("page=2") ? "Older CV" : "My CV",
              },
            ],
            totalCount: 21,
          }
        : base,
    ),
  );
  render(<MatchingPanel jobId="j" />);
  fireEvent.click(await screen.findByRole("button", { name: "Next resumes" }));
  expect(await screen.findByRole("option", { name: "Older CV" })).toBeVisible();
  expect(screen.getByRole("button", { name: "Next resumes" })).toBeDisabled();
  expect(
    within(screen.getByLabelText("Compare against")).getByRole("option", {
      name: "Career profile skills",
    }),
  ).toBeVisible();
});
