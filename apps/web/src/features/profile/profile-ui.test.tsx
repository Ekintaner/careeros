import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { beforeEach, expect, it, vi } from "vitest";
import { ProfilePage } from "./profile-page";
import { SettingsPage } from "./settings-page";
import { ApiError } from "@/lib/api-client";
import type { Profile, UserSkill } from "./profile-client";

const mocks = vi.hoisted(() => ({
  request: vi.fn(),
  submit: vi.fn(),
  accept: vi.fn(),
  passwordChanged: vi.fn(),
}));
vi.mock("@/features/auth/auth-client", () => ({ authClient: () => mocks }));
vi.mock("@/features/auth/auth-provider", () => ({ useAuth: () => mocks }));
const profile: Profile = {
  headline: null,
  summary: null,
  currentLocation: null,
  yearsOfExperience: null,
  preferredWorkMode: null,
  targetRoles: [],
  preferredLocations: [],
  employmentTypes: [],
  seniorityLevels: [],
  version: 12,
  updatedAtUtc: "2026-09-20T12:00:00Z",
};
const account = {
  id: "a",
  displayName: "Person",
  email: "person@example.com",
  emailVerified: false,
  createdAtUtc: "2026-09-20T12:00:00Z",
};
const skill: UserSkill = {
  skillId: "s",
  name: "PostgreSQL",
  proficiency: null,
  yearsExperience: null,
  version: 13,
};
beforeEach(() => {
  vi.resetAllMocks();
  mocks.request.mockImplementation((path: string, init?: RequestInit) => {
    if (path === "/api/v1/profile" && !init?.method)
      return Promise.resolve(structuredClone(profile));
    if (path === "/api/v1/profile/skills" && !init?.method)
      return Promise.resolve([]);
    if (path === "/api/v1/account" && !init?.method)
      return Promise.resolve({ account, version: 10 });
    if (path === "/api/v1/auth/me") return Promise.resolve(account);
    if (path.startsWith("/api/v1/skills?")) return Promise.resolve([]);
    return Promise.reject(new Error("Unexpected test request"));
  });
});
it("shows loading then an honest empty profile and skills", async () => {
  render(<ProfilePage />);
  expect(screen.getByRole("status")).toHaveTextContent("Loading your profile");
  expect(await screen.findByText("No skills added yet.")).toBeVisible();
  expect(screen.getByLabelText("Professional headline")).toHaveValue("");
  expect(screen.getByRole("button", { name: "Save profile" })).toBeDisabled();
});
it("offers retry after profile load failure", async () => {
  mocks.request.mockRejectedValueOnce(new Error("offline"));
  render(<ProfilePage />);
  expect(await screen.findByRole("alert")).toHaveTextContent("couldn’t load");
  fireEvent.click(screen.getByRole("button", { name: "Try again" }));
  expect(await screen.findByLabelText("Professional headline")).toBeVisible();
});
it("validates experience and preference limits before sending", async () => {
  render(<ProfilePage />);
  fireEvent.change(await screen.findByLabelText("Years of experience"), {
    target: { value: "101" },
  });
  fireEvent.change(screen.getByLabelText("Target roles"), {
    target: {
      value: Array.from({ length: 11 }, (_, i) => `Role ${i}`).join("\n"),
    },
  });
  fireEvent.click(screen.getByRole("button", { name: "Save profile" }));
  expect(
    screen.getByText("Use 0–100 years, with at most one decimal place."),
  ).toBeVisible();
  expect(
    screen.getByText("Use up to 10 entries, each up to 100 characters."),
  ).toBeVisible();
  expect(
    mocks.request.mock.calls.some(([, init]) => init?.method === "PUT"),
  ).toBe(false);
  expect(screen.getByLabelText("Years of experience")).toHaveFocus();
});
it("saves normalized profile preferences with the version and reports success", async () => {
  render(<ProfilePage />);
  fireEvent.change(await screen.findByLabelText("Professional headline"), {
    target: { value: "Engineer" },
  });
  fireEvent.change(screen.getByLabelText("Target roles"), {
    target: { value: " Backend Developer \n Data Analyst " },
  });
  fireEvent.click(screen.getByLabelText("Full Time"));
  mocks.request.mockResolvedValueOnce({
    ...profile,
    headline: "Engineer",
    targetRoles: ["Backend Developer", "Data Analyst"],
    employmentTypes: ["FullTime"],
    version: 14,
  });
  fireEvent.click(screen.getByRole("button", { name: "Save profile" }));
  expect(await screen.findByText("Profile saved.")).toBeVisible();
  const call = mocks.request.mock.calls.find(
    ([, init]) => init?.method === "PUT",
  )!;
  expect(JSON.parse(call[1].body)).toMatchObject({
    version: 12,
    targetRoles: ["Backend Developer", "Data Analyst"],
    employmentTypes: ["FullTime"],
  });
  expect(JSON.parse(call[1].body)).not.toHaveProperty("userId");
  expect(screen.getByRole("button", { name: "Save profile" })).toBeDisabled();
});
it("keeps edits after a conflict and exposes deliberate reload", async () => {
  render(<ProfilePage />);
  fireEvent.change(await screen.findByLabelText("Professional headline"), {
    target: { value: "Unsaved work" },
  });
  mocks.request.mockRejectedValueOnce(
    new ApiError(409, {
      title: "This record changed. Reload before editing again.",
    }),
  );
  fireEvent.click(screen.getByRole("button", { name: "Save profile" }));
  expect(await screen.findByRole("alert")).toHaveTextContent(
    "This record changed",
  );
  expect(screen.getByLabelText("Professional headline")).toHaveValue(
    "Unsaved work",
  );
  expect(
    screen.getByRole("button", { name: "Reload latest profile" }),
  ).toBeVisible();
});
it("discards unsaved edits without making a mutation", async () => {
  render(<ProfilePage />);
  fireEvent.change(await screen.findByLabelText("Professional headline"), {
    target: { value: "Unsaved" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Discard changes" }));
  expect(screen.getByLabelText("Professional headline")).toHaveValue("");
  expect(screen.getByRole("button", { name: "Save profile" })).toBeDisabled();
});
it("adds and removes an owned skill using its version", async () => {
  render(<ProfilePage />);
  const input = await screen.findByLabelText("Find or add a skill");
  fireEvent.change(input, { target: { value: "PostgreSQL" } });
  mocks.request.mockResolvedValueOnce(skill);
  fireEvent.click(screen.getByRole("button", { name: "Add skill" }));
  expect(await screen.findByText("PostgreSQL added.")).toBeVisible();
  mocks.request.mockResolvedValueOnce(undefined);
  fireEvent.click(screen.getByRole("button", { name: "Remove PostgreSQL" }));
  expect(await screen.findByText("PostgreSQL removed.")).toBeVisible();
  expect(screen.getByText("No skills added yet.")).toBeVisible();
  expect(mocks.request).toHaveBeenCalledWith(
    "/api/v1/profile/skills/s?version=13",
    expect.objectContaining({ method: "DELETE" }),
  );
});
it("updates skill metadata and reports its success", async () => {
  mocks.request.mockImplementation((path: string) =>
    Promise.resolve(path === "/api/v1/profile" ? profile : [skill]),
  );
  render(<ProfilePage />);
  fireEvent.change(await screen.findByLabelText("Proficiency for PostgreSQL"), {
    target: { value: "Expert" },
  });
  mocks.request.mockResolvedValueOnce({
    ...skill,
    proficiency: "Expert",
    version: 15,
  });
  fireEvent.click(screen.getByRole("button", { name: "Save skill" }));
  expect(await screen.findByText("Skill updated.")).toBeVisible();
});
it("does not remove a skill when the server rejects the deletion", async () => {
  mocks.request.mockImplementation((path: string) =>
    Promise.resolve(path === "/api/v1/profile" ? profile : [skill]),
  );
  render(<ProfilePage />);
  await screen.findByRole("button", { name: "Remove PostgreSQL" });
  mocks.request.mockRejectedValueOnce(
    new ApiError(409, { title: "Record changed" }),
  );
  fireEvent.click(screen.getByRole("button", { name: "Remove PostgreSQL" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Record changed");
  expect(
    screen.getByRole("button", { name: "Remove PostgreSQL" }),
  ).toBeVisible();
});
it("renders safe account settings with honest verification status", async () => {
  render(<SettingsPage />);
  expect(await screen.findByText(account.email)).toBeVisible();
  expect(
    screen.getByText("Not verified · delivery is not available yet"),
  ).toBeVisible();
  expect(
    screen.queryByRole("button", { name: /send verification/i }),
  ).not.toBeInTheDocument();
  expect(screen.getByLabelText("Display name")).toHaveValue("Person");
});
it("validates and saves the display name and updates live account state", async () => {
  render(<SettingsPage />);
  const name = await screen.findByLabelText("Display name");
  fireEvent.change(name, { target: { value: " " } });
  fireEvent.click(screen.getByRole("button", { name: "Save account" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Enter your name");
  fireEvent.change(name, { target: { value: "Updated" } });
  mocks.request.mockResolvedValueOnce({
    account: { ...account, displayName: "Updated" },
    version: 11,
  });
  fireEvent.click(screen.getByRole("button", { name: "Save account" }));
  expect(await screen.findByText("Account updated.")).toBeVisible();
  expect(mocks.accept).toHaveBeenCalledWith({
    ...account,
    displayName: "Updated",
  });
});
it("validates password confirmation and current password without submitting", async () => {
  render(<SettingsPage />);
  await screen.findByLabelText("Current password");
  fireEvent.change(screen.getByLabelText("New password"), {
    target: { value: "short" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Change password" }));
  expect(screen.getByText("Enter your current password.")).toBeVisible();
  expect(screen.getByText("Your passwords do not match.")).toBeVisible();
  expect(mocks.submit).not.toHaveBeenCalled();
});
it("reuses secure password change and clears credentials before ending the session", async () => {
  render(<SettingsPage />);
  await screen.findByLabelText("Current password");
  fireEvent.change(screen.getByLabelText("Current password"), {
    target: { value: "old memorable passphrase" },
  });
  fireEvent.change(screen.getByLabelText("New password"), {
    target: { value: "new memorable passphrase" },
  });
  fireEvent.change(screen.getByLabelText("Confirm new password"), {
    target: { value: "new memorable passphrase" },
  });
  mocks.submit.mockResolvedValue(undefined);
  fireEvent.click(screen.getByRole("button", { name: "Change password" }));
  await waitFor(() => expect(mocks.passwordChanged).toHaveBeenCalled());
  expect(mocks.submit).toHaveBeenCalledWith("change-password", {
    currentPassword: "old memorable passphrase",
    newPassword: "new memorable passphrase",
  });
  expect(screen.getByLabelText("Current password")).toHaveValue("");
});
it("keeps account settings usable after an incorrect current password", async () => {
  render(<SettingsPage />);
  await screen.findByLabelText("Current password");
  for (const [name, value] of [
    ["Current password", "incorrect"],
    ["New password", "new memorable passphrase"],
    ["Confirm new password", "new memorable passphrase"],
  ] as const)
    fireEvent.change(screen.getByLabelText(name), { target: { value } });
  mocks.submit.mockRejectedValue(
    new ApiError(401, { title: "Invalid current password." }),
  );
  fireEvent.click(screen.getByRole("button", { name: "Change password" }));
  expect(await screen.findByRole("alert")).toHaveTextContent(
    "Invalid current password",
  );
  expect(mocks.passwordChanged).not.toHaveBeenCalled();
  expect(
    within(
      screen.getByRole("region", { name: "Personal details" }),
    ).getByLabelText("Display name"),
  ).toBeEnabled();
});
