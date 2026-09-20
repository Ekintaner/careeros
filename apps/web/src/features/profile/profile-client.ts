import { authClient, type Account } from "@/features/auth/auth-client";
import { ApiError } from "@/lib/api-client";

export const workModes = ["OnSite", "Hybrid", "Remote"] as const;
export const employmentTypes = [
  "FullTime",
  "PartTime",
  "Contract",
  "Temporary",
  "Internship",
  "Apprenticeship",
  "Other",
] as const;
export const seniorityLevels = [
  "Internship",
  "Entry",
  "Associate",
  "Mid",
  "Senior",
  "Lead",
  "Executive",
  "Other",
] as const;
export const proficiencies = [
  "Beginner",
  "Intermediate",
  "Advanced",
  "Expert",
] as const;
export const label = (value: string) =>
  value.replace(/([a-z])([A-Z])/g, "$1 $2");
export interface Profile {
  headline: string | null;
  summary: string | null;
  currentLocation: string | null;
  yearsOfExperience: number | null;
  preferredWorkMode: string | null;
  targetRoles: string[];
  preferredLocations: string[];
  employmentTypes: string[];
  seniorityLevels: string[];
  version: number;
  updatedAtUtc: string | null;
}
export interface UserSkill {
  skillId: string;
  name: string;
  proficiency: string | null;
  yearsExperience: number | null;
  version: number;
}
export interface Suggestion {
  id: string;
  name: string;
}
export interface AccountSettings {
  account: Account;
  version: number;
}
export async function read<T>(path: string, signal?: AbortSignal): Promise<T> {
  const result = await authClient().request<T>(`/api/v1/${path}`, { signal });
  if (result === undefined) throw new Error("Missing response");
  return result;
}
export async function write<T>(
  path: string,
  method: string,
  body?: unknown,
): Promise<T | undefined> {
  return authClient().request<T>(`/api/v1/${path}`, {
    method,
    headers: { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
}
export function failure(error: unknown) {
  return error instanceof ApiError && error.status < 500
    ? error.message
    : "We couldn’t save your changes. Please try again.";
}
export function experienceError(value: string) {
  return value !== "" &&
    (!/^\d{1,3}(\.\d)?$/.test(value) || Number(value) > 100)
    ? "Use 0–100 years, with at most one decimal place."
    : "";
}
export function lines(value: string) {
  return value
    .split("\n")
    .map((v) => v.trim())
    .filter(Boolean);
}
export function preferenceError(value: string, max: number) {
  const items = lines(value);
  return items.length > 10 || items.some((v) => v.length > max)
    ? `Use up to 10 entries, each up to ${max} characters.`
    : "";
}
