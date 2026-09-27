export {
  request,
  failure,
  date,
  safeUrl,
  type Page,
} from "@/features/opportunities/client";
export const stages = [
  "Saved",
  "Applied",
  "Assessment",
  "Interview",
  "Offer",
] as const;
export const outcomes = ["Rejected", "Withdrawn", "Accepted"] as const;
export const statuses = [...stages, ...outcomes, "Archived"] as const;
export type Status = (typeof statuses)[number];
export interface ApplicationCard {
  id: string;
  jobId: string;
  companyId: string;
  jobTitle: string;
  companyName: string;
  location: string | null;
  status: Status;
  resumeId: string | null;
  appliedAtUtc: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  version: number;
}
export interface ApplicationView extends ApplicationCard {
  resumeName: string | null;
  source: string | null;
  sourceUrl: string | null;
  lastStatusChangedAtUtc: string;
}
export interface History {
  id: string;
  fromStatus: Status | null;
  toStatus: Status;
  sequence: number;
  changedAtUtc: string;
}
export interface Note {
  id: string;
  body: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  version: number;
}
export interface StageCount {
  status: Status;
  count: number;
}
export function nextStatuses(status: Status): Status[] {
  if (status === "Archived" || outcomes.some((s) => s === status)) return [];
  if (status === "Saved") return ["Applied", "Withdrawn"];
  return [
    ...stages.filter((s) => s !== "Saved" && s !== status),
    ...(status === "Offer" ? ["Accepted" as const] : []),
    "Rejected",
    "Withdrawn",
  ];
}
