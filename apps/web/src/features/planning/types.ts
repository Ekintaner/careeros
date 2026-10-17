export const interviewTypes = [
  "Recruiter",
  "HR",
  "Technical",
  "Behavioral",
  "HiringManager",
  "CaseStudy",
  "Final",
  "Other",
];
export interface Interview {
  id: string;
  applicationId: string;
  type: string;
  scheduledStartUtc: string;
  scheduledEndUtc: string;
  location: string | null;
  meetingUrl: string | null;
  outcome: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  version: number;
}
export interface CareerTask {
  id: string;
  applicationId: string | null;
  title: string;
  description: string | null;
  dueAtUtc: string | null;
  priority: string;
  completedAtUtc: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  version: number;
}
export function inputTime(
  utc: string | null | undefined,
  basis: string,
): string {
  if (!utc) return "";
  const d = new Date(utc);
  if (basis === "UTC") return d.toISOString().slice(0, 16);
  return new Date(d.getTime() - d.getTimezoneOffset() * 60000)
    .toISOString()
    .slice(0, 16);
}
export function utcTime(value: string, basis: string): string | null {
  if (!value) return null;
  const d = new Date(basis === "UTC" ? value + "Z" : value);
  if (
    !Number.isFinite(d.getTime()) ||
    inputTime(d.toISOString(), basis) !== value
  )
    throw new Error(
      "This time is invalid or skipped by daylight saving. Choose another time or use UTC.",
    );
  if (basis !== "UTC") {
    for (const minutes of [-120, -60, -30, 30, 60, 120]) {
      if (
        inputTime(
          new Date(d.getTime() + minutes * 60000).toISOString(),
          basis,
        ) === value
      )
        throw new Error(
          "This local time occurs twice during daylight saving. Use UTC to choose the exact instant.",
        );
    }
  }
  return d.toISOString();
}
export function timestamp(value: string | null) {
  return value
    ? new Intl.DateTimeFormat(undefined, {
        dateStyle: "medium",
        timeStyle: "short",
      }).format(new Date(value))
    : "No due date";
}
