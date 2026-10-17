import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { beforeEach, expect, it, vi } from "vitest";
import { AssistancePage, type Assistance } from "./assistance-page";
import { ApiError } from "@/lib/api-client";
const mocks = vi.hoisted(() => ({ request: vi.fn() }));
vi.mock("@/features/auth/auth-client", () => ({ authClient: () => mocks }));
const result: Assistance = {
  label: "AI-selected suggestions · verify before use",
  generatedAtUtc: "2026-10-17T12:00:00Z",
  model: "test:1b",
  facts: [],
  responsibilities: ["Build APIs."],
  qualifications: ["SQL"],
  suggestions: ["Prepare actual examples."],
  questions: [],
};
beforeEach(() => {
  mocks.request.mockReset();
});
function setup(
  state = "Ready",
  generate: () => Promise<Assistance> = () => Promise.resolve(result),
) {
  mocks.request.mockImplementation((path: string, init?: RequestInit) => {
    if (path.endsWith("/ai/status"))
      return Promise.resolve({
        state,
        message:
          state === "Ready" ? "Verified local model" : "Local AI is off.",
        model: state === "Ready" ? "test:1b" : null,
      });
    if (path.includes("resume-options"))
      return Promise.resolve({
        items: [{ id: "resume-1", name: "Recorded CV" }],
        page: 1,
        pageSize: 20,
        totalCount: 1,
      });
    if (path.endsWith("/assistance")) {
      expect(init?.method).toBe("POST");
      return generate();
    }
    return Promise.resolve({
      id: "job-1",
      title: "Engineer",
      description: "Build APIs. SQL",
    });
  });
}
it("keeps the disabled experience honest and never generates on page load", async () => {
  setup("Disabled");
  render(<AssistancePage jobId="job-1" />);
  await screen.findByText("Local AI is off.");
  expect(
    screen.getByRole("button", { name: "Generate local suggestions" }),
  ).toBeDisabled();
  expect(screen.getByText("Skills Intelligence")).toHaveAttribute(
    "href",
    "/skills",
  );
  expect(
    mocks.request.mock.calls.some(([p]) => p.endsWith("/assistance")),
  ).toBe(false);
});
it("requires consent and displays grounded output as plain text", async () => {
  setup("Ready", () =>
    Promise.resolve({
      ...result,
      responsibilities: ["<script>untrusted description</script>"],
    }),
  );
  render(<AssistancePage jobId="job-1" />);
  await screen.findByText("Verified local model");
  const button = screen.getByRole("button", {
    name: "Generate local suggestions",
  });
  expect(button).toBeDisabled();
  fireEvent.click(screen.getByRole("checkbox"));
  fireEvent.click(button);
  await screen.findByRole("region", { name: "AI assistance result" });
  expect(
    screen.getByText("<script>untrusted description</script>"),
  ).toBeVisible();
  expect(document.querySelector("script")).toBeNull();
  expect(screen.getByText(/AI-selected suggestions/)).toBeVisible();
  const call = mocks.request.mock.calls.find(([p]) =>
    p.endsWith("/assistance"),
  );
  expect(JSON.parse(call?.[1].body)).toEqual({
    mode: "job",
    resumeId: null,
    consent: true,
  });
});
it("resume help requires explicit selection and separates recorded facts", async () => {
  setup("Ready", () =>
    Promise.resolve({
      ...result,
      facts: [{ id: "r1", source: "selected resume skill", value: "SQL" }],
      responsibilities: [],
      qualifications: [],
      suggestions: ["Only add a verified example."],
    }),
  );
  render(<AssistancePage jobId="job-1" />);
  await screen.findByText("Verified local model");
  fireEvent.click(screen.getByRole("radio", { name: "Resume improvement" }));
  await screen.findByText("Recorded CV");
  fireEvent.click(screen.getByRole("checkbox"));
  expect(
    screen.getByRole("button", { name: "Generate local suggestions" }),
  ).toBeDisabled();
  fireEvent.change(screen.getByLabelText("Selected resume"), {
    target: { value: "resume-1" },
  });
  expect(screen.getByRole("checkbox")).not.toBeChecked();
  fireEvent.click(screen.getByRole("checkbox"));
  fireEvent.click(
    screen.getByRole("button", { name: "Generate local suggestions" }),
  );
  await screen.findByText("Recorded facts used · user supplied");
  expect(screen.getByText("selected resume skill")).toBeVisible();
  expect(screen.getByText("SQL")).toBeVisible();
});
it("does not show late results after cancel", async () => {
  let resolve!: (value: Assistance) => void;
  setup(
    "Ready",
    () =>
      new Promise((r) => {
        resolve = r;
      }),
  );
  render(<AssistancePage jobId="job-1" />);
  await screen.findByText("Verified local model");
  fireEvent.click(screen.getByRole("checkbox"));
  fireEvent.click(
    screen.getByRole("button", { name: "Generate local suggestions" }),
  );
  await screen.findByText("Preparing locally…");
  fireEvent.click(screen.getByRole("button", { name: "Cancel" }));
  resolve(result);
  await waitFor(() =>
    expect(
      screen.queryByRole("region", { name: "AI assistance result" }),
    ).toBeNull(),
  );
  expect(screen.getByRole("alert")).toHaveTextContent("cancelled");
});
it("shows an unavailable model error without inventing results", async () => {
  setup("Ready", () =>
    Promise.reject(
      new ApiError(503, { title: "Local AI timed out after 90 seconds." }),
    ),
  );
  render(<AssistancePage jobId="job-1" />);
  await screen.findByText("Verified local model");
  fireEvent.click(screen.getByRole("checkbox"));
  fireEvent.click(
    screen.getByRole("button", { name: "Generate local suggestions" }),
  );
  expect(await screen.findByRole("alert")).toHaveTextContent("timed out");
  expect(
    screen.queryByRole("region", { name: "AI assistance result" }),
  ).toBeNull();
});
