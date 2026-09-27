"use client";
import { useEffect, useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ApiError } from "@/lib/api-client";
import { Field, Feedback, useUnsavedChanges } from "@/features/profile/form-ui";
import { useResource } from "@/features/opportunities/lists";
import { type Job, type JobSummary } from "@/features/opportunities/client";
import {
  request,
  failure,
  type ApplicationView,
  type ApplicationCard,
  type Page,
} from "./client";

function Lookup({
  kind,
  value,
  selectedName,
  onChange,
  disabled,
}: {
  kind: "job" | "resume";
  value: string;
  selectedName?: string;
  onChange: (value: string) => void;
  disabled: boolean;
}) {
  const [search, setSearch] = useState("");
  const [query, setQuery] = useState("");
  useEffect(() => {
    const timer = setTimeout(() => setQuery(search), 200);
    return () => clearTimeout(timer);
  }, [search]);
  const [attempt, setAttempt] = useState(0);
  const path =
    kind === "job"
      ? `jobs?state=all&pageSize=20&search=${encodeURIComponent(query)}`
      : `applications/resume-options?search=${encodeURIComponent(query)}`;
  const resource = useResource<Page<JobSummary & { name: string }>>(
    path,
    attempt,
  );
  const options = resource?.data?.items ?? [];
  return (
    <div>
      <Field
        id={`${kind}-search`}
        title={`Find ${kind === "job" ? "a job" : "a resume"}`}
      >
        <input
          id={`${kind}-search`}
          value={search}
          maxLength={200}
          disabled={disabled}
          onChange={(e) => setSearch(e.target.value)}
        />
      </Field>
      <Field
        id={`${kind}-choice`}
        title={kind === "job" ? "Job" : "Resume (optional)"}
      >
        <select
          id={`${kind}-choice`}
          value={value}
          required={kind === "job"}
          disabled={disabled || !resource || !!resource.error}
          onChange={(e) => onChange(e.target.value)}
        >
          <option value="">
            {kind === "job" ? "Choose an existing job" : "No resume"}
          </option>
          {value && !options.some((x) => x.id === value) && (
            <option value={value}>{selectedName || "Selected record"}</option>
          )}
          {options.map((x) => (
            <option value={x.id} key={x.id}>
              {kind === "job" ? `${x.title} · ${x.companyName}` : x.name}
            </option>
          ))}
        </select>
      </Field>
      {!resource ? (
        <p role="status">Loading {kind}s…</p>
      ) : resource.error ? (
        <>
          <Feedback error={resource.error} />
          <button
            type="button"
            className="text-button"
            onClick={() => setAttempt((n) => n + 1)}
          >
            Retry {kind} search
          </button>
        </>
      ) : !options.length ? (
        <p className="field-hint">
          {kind === "resume" && !query
            ? "No resumes available yet."
            : `No ${kind}s match this search.`}
        </p>
      ) : (
        resource.data!.totalCount > 20 && (
          <p className="field-hint">
            Showing 20 matches. Refine your search to find another {kind}.
          </p>
        )
      )}
      {kind === "job" && (
        <Link className="back-link" href="/jobs/new">
          Add a job first
        </Link>
      )}
    </div>
  );
}
function Editor({
  application,
  initialJob,
}: {
  application?: ApplicationView;
  initialJob?: Job;
}) {
  const router = useRouter();
  const [jobId, setJob] = useState(initialJob?.id ?? "");
  const [initialStatus, setStatus] = useState("Saved");
  const [resume, setResume] = useState(application?.resumeId ?? "");
  const [source, setSource] = useState(application?.source ?? "");
  const [applied, setApplied] = useState(
    application?.appliedAtUtc?.slice(0, 10) ?? "",
  );
  const [dirty, setDirty] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [existing, setExisting] = useState("");
  const [conflict, setConflict] = useState(false);
  useUnsavedChanges(dirty);
  const canDate = application
    ? !!application.appliedAtUtc
    : initialStatus === "Applied";
  async function save(e: FormEvent) {
    e.preventDefault();
    if (busy) return;
    if (!application && !jobId) {
      setError("Choose an existing job.");
      return;
    }
    if (
      source.trim().length > 120 ||
      (canDate &&
        applied &&
        (applied < "1900-01-01" ||
          applied > new Date().toISOString().slice(0, 10)))
    ) {
      setError("Check the source length and applied date.");
      return;
    }
    setBusy(true);
    setError("");
    try {
      // Preserve exact stored time when the displayed date was not deliberately changed.
      const appliedAtUtc =
        canDate && applied
          ? application?.appliedAtUtc?.slice(0, 10) === applied
            ? application.appliedAtUtc
            : `${applied}T00:00:00Z`
          : null;
      const body = {
        source: source.trim() || null,
        resumeId: resume || null,
        appliedAtUtc,
      };
      const result = await request<ApplicationView>(
        application ? `applications/${application.id}` : "applications",
        application ? "PUT" : "POST",
        application
          ? { ...body, version: application.version }
          : { ...body, jobId, initialStatus },
      );
      setDirty(false);
      router.push(`/applications/${result.id}`);
    } catch (e) {
      setError(failure(e));
      if (e instanceof ApiError && e.status === 409) {
        setConflict(true);
        if (!application) {
          try {
            const found = await request<Page<ApplicationCard>>(
              `applications?jobId=${encodeURIComponent(jobId)}&state=all&pageSize=1`,
            );
            setExisting(found.items[0]?.id ?? "");
          } catch {
            /* Original conflict remains visible; no automatic write retry. */
          }
        }
      }
    } finally {
      setBusy(false);
    }
  }
  return (
    <form onSubmit={save} onChange={() => setDirty(true)}>
      <Feedback error={error} />
      <section className="editor-section">
        <div>
          <h2>{application ? "Application details" : "Start tracking"}</h2>
          <p>
            Job details stay with the opportunity. Keep application-specific
            information here.
          </p>
        </div>
        <div className="section-fields">
          {application ? (
            <p>
              <strong>{application.jobTitle}</strong>
              <br />
              {application.companyName}
            </p>
          ) : initialJob ? (
            <p>
              <strong>{initialJob.title}</strong>
              <br />
              {initialJob.companyName}
            </p>
          ) : (
            <Lookup
              kind="job"
              value={jobId}
              onChange={setJob}
              disabled={busy}
            />
          )}
          {!application && (
            <Field id="initial-stage" title="Initial status">
              <select
                id="initial-stage"
                value={initialStatus}
                disabled={busy}
                onChange={(e) => setStatus(e.target.value)}
              >
                <option>Saved</option>
                <option>Applied</option>
              </select>
            </Field>
          )}
          {canDate && (
            <Field
              id="application-date"
              title="Applied date (UTC)"
              hint={
                application
                  ? "Correct the date without rewriting status history."
                  : "Leave empty to use the current time."
              }
            >
              <input
                id="application-date"
                type="date"
                min="1900-01-01"
                max={(
                  application?.lastStatusChangedAtUtc ??
                  new Date().toISOString()
                ).slice(0, 10)}
                required={!!application}
                value={applied}
                disabled={busy}
                onChange={(e) => setApplied(e.target.value)}
              />
            </Field>
          )}
          <Field id="application-source" title="Application source (optional)">
            <input
              id="application-source"
              value={source}
              maxLength={120}
              disabled={busy}
              onChange={(e) => setSource(e.target.value)}
            />
          </Field>
          <Lookup
            kind="resume"
            value={resume}
            selectedName={application?.resumeName ?? undefined}
            onChange={setResume}
            disabled={busy}
          />
        </div>
      </section>
      <div className="editor-actions">
        <button className="primary-button" disabled={busy || conflict}>
          {busy
            ? "Saving…"
            : application
              ? "Save application"
              : "Create application"}
        </button>
        <button
          type="button"
          className="secondary-button"
          disabled={busy}
          onClick={() => {
            if (!dirty || window.confirm("Discard unsaved changes?"))
              router.push(
                application
                  ? `/applications/${application.id}`
                  : "/applications",
              );
          }}
        >
          Cancel
        </button>
      </div>
      {existing && (
        <Link className="back-link" href={`/applications/${existing}`}>
          View existing application
        </Link>
      )}
      {conflict && (
        <button
          className="text-button"
          type="button"
          onClick={() => {
            if (
              !dirty ||
              window.confirm(
                "Discard this draft and reload the latest application?",
              )
            )
              window.location.reload();
          }}
        >
          Reload latest application
        </button>
      )}
    </form>
  );
}
export function ApplicationEditor({
  id,
  jobId,
}: {
  id?: string;
  jobId?: string;
}) {
  const [attempt, setAttempt] = useState(0);
  const resource = useResource<ApplicationView | Job>(
    id
      ? `applications/${id}`
      : jobId
        ? `jobs/${encodeURIComponent(jobId)}`
        : "applications/resume-options",
    attempt,
  );
  return (
    <div className="editor-page">
      <Link
        className="back-link"
        href={id ? `/applications/${id}` : "/applications"}
      >
        ← Applications
      </Link>
      <header className="page-heading">
        <p className="eyebrow">APPLICATION TRACKING</p>
        <h1>{id ? "Edit application" : "Track application"}</h1>
      </header>
      {(id || jobId) && !resource ? (
        <p role="status">Loading application…</p>
      ) : (id || jobId) && resource?.error ? (
        <>
          <Feedback error={resource.error} />
          <button
            className="secondary-button"
            onClick={() => setAttempt((n) => n + 1)}
          >
            Retry
          </button>
        </>
      ) : (
        <Editor
          application={id ? (resource?.data as ApplicationView) : undefined}
          initialJob={jobId ? (resource?.data as Job) : undefined}
        />
      )}
    </div>
  );
}
