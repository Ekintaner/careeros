import { authClient } from "@/features/auth/auth-client";
import { ApiError } from "@/lib/api-client";
export {
  workModes,
  employmentTypes,
  seniorityLevels,
  label,
} from "@/features/profile/profile-client";
export interface Page<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
export interface CompanySummary {
  id: string;
  name: string;
  industry: string | null;
  location: string | null;
  jobCount: number;
  version: number;
}
export interface Company extends Omit<CompanySummary, "jobCount"> {
  website: string | null;
  description: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}
export interface JobSummary {
  id: string;
  companyId: string;
  companyName: string;
  title: string;
  location: string | null;
  workMode: string | null;
  employmentType: string | null;
  seniorityLevel: string | null;
  source: string | null;
  createdAtUtc: string;
  postedAtUtc: string | null;
  archivedAtUtc: string | null;
  version: number;
}
export interface Job extends JobSummary {
  description: string | null;
  sourceUrl: string | null;
  salaryMin: number | null;
  salaryMax: number | null;
  salaryCurrency: string | null;
  salaryPeriod: string | null;
  expiresAtUtc: string | null;
  updatedAtUtc: string;
}
export async function request<T>(
  path: string,
  method?: string,
  body?: unknown,
  signal?: AbortSignal,
): Promise<T> {
  return (await authClient().request<T>(`/api/v1/${path}`, {
    method,
    signal,
    headers: { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  })) as T;
}
export function failure(error: unknown) {
  return error instanceof ApiError && error.status < 500
    ? error.status === 404
      ? "This record is no longer available."
      : error.message
    : "We couldn’t complete this request. Please try again.";
}
export function safeUrl(value: string | null): string | undefined {
  if (!value || /\s/.test(value)) return undefined;
  try {
    const url = new URL(value);
    return ["http:", "https:"].includes(url.protocol) &&
      url.hostname &&
      !url.username &&
      !url.password
      ? value
      : undefined;
  } catch {
    return undefined;
  }
}
export const date = (value: string) =>
  new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeZone: "UTC",
  }).format(new Date(value));
export const empty = (value: string) => value.trim() || null;
