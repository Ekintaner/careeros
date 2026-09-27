"use client";
import { useState } from "react";
import Link from "next/link";
import { useResource } from "@/features/opportunities/lists";
import { type Page, type ApplicationCard } from "./client";
export function JobApplicationLink({ jobId }: { jobId: string }) {
  const [attempt, setAttempt] = useState(0);
  const resource = useResource<Page<ApplicationCard>>(
    `applications?jobId=${jobId}&state=all&pageSize=1`,
    attempt,
  );
  if (!resource) return <span role="status">Checking application…</span>;
  if (resource.error)
    return (
      <button
        className="secondary-button"
        onClick={() => setAttempt((n) => n + 1)}
      >
        Retry application lookup
      </button>
    );
  const existing = resource.data?.items[0];
  return (
    <Link
      className="secondary-button"
      href={
        existing
          ? `/applications/${existing.id}`
          : `/applications/new?jobId=${jobId}`
      }
    >
      {existing ? "View application" : "Track application"}
    </Link>
  );
}
