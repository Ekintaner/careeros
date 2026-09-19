export type IconName =
  | "overview"
  | "applications"
  | "jobs"
  | "companies"
  | "interviews"
  | "resumes"
  | "skills"
  | "analytics"
  | "tasks"
  | "documents"
  | "settings";

const paths: Record<IconName, string> = {
  overview: "M3 3h7v7H3z M14 3h7v7h-7z M3 14h7v7H3z M14 14h7v7h-7z",
  applications: "M3 5h18v15H3z M8 5V3h8v2 M3 11h18 M10 11v3h4v-3",
  jobs: "M5 4h14v17H5z M9 8h6 M9 12h6 M9 16h4",
  companies: "M4 21V3h12v18 M16 10h4v11 M8 7h4 M8 11h4 M8 15h4 M2 21h20",
  interviews: "M3 5h18v16H3z M7 3v4 M17 3v4 M3 10h18 M7 14h3 M14 14h3",
  resumes: "M5 3h10l4 4v14H5z M14 3v5h5 M9 12h6 M9 16h6",
  skills: "m12 3 9 5-9 5-9-5z M3 12l9 5 9-5 M3 16l9 5 9-5",
  analytics: "M4 3v18h17 M8 17v-5 M13 17V8 M18 17V4",
  tasks: "M9 6h12 M9 12h12 M9 18h12 M3 5l1 1 2-2 M3 11l1 1 2-2 M3 17l1 1 2-2",
  documents: "M3 6h7l2 2h9v12H3z M3 6V4h7l2 2",
  settings:
    "M9 3h6l1 4 4 2v6l-4 2-1 4H9l-1-4-4-2V9l4-2z M9 12a3 3 0 1 0 6 0 3 3 0 1 0-6 0",
};

export function Icon({ name, size = 18 }: { name: IconName; size?: number }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.5"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d={paths[name]} />
    </svg>
  );
}
