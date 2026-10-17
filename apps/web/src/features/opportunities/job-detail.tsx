"use client";
import { useState } from "react";
import { MatchingPanel } from "@/features/matching/matching-panel";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Feedback } from "@/features/profile/form-ui";
import { request, failure, safeUrl, date, label, type Job } from "./client";
import { useResource } from "./lists";
import { JobApplicationLink } from "@/features/applications/job-link";

export function JobDetail({
  id,
  saved = false,
}: {
  id: string;
  saved?: boolean;
}) {
  const router = useRouter();
  const [attempt, setAttempt] = useState(0);
  const state = useResource<Job>(`jobs/${id}`, attempt);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState(saved ? "Job saved." : "");
  const j = state?.data;
  async function archive() {
    if (!j || busy) return;
    setBusy(true);
    setError("");
    try {
      await request(`jobs/${id}/archive`, "PUT", {
        archived: !j.archivedAtUtc,
        version: j.version,
      });
      setSuccess(j.archivedAtUtc ? "Job restored." : "Job archived.");
      setAttempt(attempt + 1);
    } catch (e) {
      setError(failure(e));
    } finally {
      setBusy(false);
    }
  }
  async function remove() {
    if (
      !j ||
      busy ||
      !window.confirm(
        `Delete ${j.title}? Jobs with application history cannot be deleted.`,
      )
    )
      return;
    setBusy(true);
    setError("");
    try {
      await request(`jobs/${id}?version=${j.version}`, "DELETE");
      router.push("/jobs");
    } catch (e) {
      setError(failure(e));
      setBusy(false);
    }
  }
  return (
    <div className="editor-page">
      <Link className="back-link" href="/jobs">
        ← Jobs
      </Link>
      <Feedback error={error} success={success} />
      {!state ? (
        <p role="status">Loading job…</p>
      ) : state.error ? (
        <Feedback error={state.error} />
      ) : (
        j && (
          <>
            <header className="page-heading job-heading">
              <div>
                <p className="eyebrow">{j.companyName}</p>
                <h1>{j.title}</h1>
                <p>
                  {[j.location, j.workMode && label(j.workMode)]
                    .filter(Boolean)
                    .join(" · ")}
                </p>
              </div>
              {j.archivedAtUtc && (
                <span className="outline-label">Archived</span>
              )}
            </header>
            <div className="detail-actions">
              <JobApplicationLink jobId={id} />
              <Link className="primary-button" href={`/jobs/${id}/edit`}>
                Edit job
              </Link>
              <button
                className="secondary-button"
                disabled={busy}
                onClick={() => void archive()}
              >
                {j.archivedAtUtc ? "Restore job" : "Archive job"}
              </button>
              {safeUrl(j.sourceUrl) && (
                <a
                  className="secondary-button"
                  href={safeUrl(j.sourceUrl)}
                  target="_blank"
                  rel="noopener noreferrer"
                >
                  Open original posting ↗
                </a>
              )}
              <button
                className="text-button danger-text"
                disabled={busy}
                onClick={() => void remove()}
              >
                Delete job
              </button>
            </div>
            <MatchingPanel key={id} jobId={id} />
            <dl className="job-facts">
              {(
                [
                  ["Employment", j.employmentType && label(j.employmentType)],
                  ["Seniority", j.seniorityLevel && label(j.seniorityLevel)],
                  ["Source", j.source],
                  [
                    "Compensation",
                    j.salaryMin !== null || j.salaryMax !== null
                      ? `${j.salaryMin === null ? "Up to " : j.salaryMax === null ? "From " : ""}${j.salaryMin?.toLocaleString() ?? ""}${j.salaryMin !== null && j.salaryMax !== null ? " – " : ""}${j.salaryMax?.toLocaleString() ?? ""} ${j.salaryCurrency} / ${j.salaryPeriod?.toLowerCase()}`
                      : null,
                  ],
                  ["Posted", j.postedAtUtc && date(j.postedAtUtc)],
                  ["Expires", j.expiresAtUtc && date(j.expiresAtUtc)],
                  ["Saved", date(j.createdAtUtc)],
                  ["Updated", date(j.updatedAtUtc)],
                ] as const
              )
                .filter(([, v]) => v)
                .map(([title, value]) => (
                  <div key={title}>
                    <dt>{title}</dt>
                    <dd>{value}</dd>
                  </div>
                ))}
            </dl>
            {j.description ? (
              <section className="job-description">
                <h2>About the opportunity</h2>
                <p>{j.description}</p>
              </section>
            ) : (
              <p className="field-hint">
                No description added. Edit this job to keep the posting details
                here.
              </p>
            )}
          </>
        )
      )}
      {(error || state?.error) && (
        <button
          className="secondary-button"
          disabled={busy}
          onClick={() => {
            setError("");
            setAttempt(attempt + 1);
          }}
        >
          Reload latest job
        </button>
      )}
    </div>
  );
}
