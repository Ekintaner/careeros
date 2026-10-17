"use client";
import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import {
  request,
  failure,
  type Job,
  type Page,
} from "@/features/opportunities/client";
import { useResource } from "@/features/opportunities/lists";
import { ApiError } from "@/lib/api-client";
import "./assistance.css";
interface Availability {
  state: string;
  message: string;
  model: string | null;
}
export interface Assistance {
  label: string;
  generatedAtUtc: string;
  model: string;
  facts: { id: string; source: string; value: string }[];
  responsibilities: string[];
  qualifications: string[];
  suggestions: string[];
  questions: string[];
}
function ResultList({ title, items }: { title: string; items: string[] }) {
  return (
    <section className="ai-section">
      <h3>{title}</h3>
      {items.length ? (
        <ul>
          {items.map((x, i) => (
            <li key={i}>{x}</li>
          ))}
        </ul>
      ) : (
        <p>No supported items were selected from the saved information.</p>
      )}
    </section>
  );
}
function ResumeChoice({
  value,
  change,
  disabled,
}: {
  value: string;
  change: (v: string) => void;
  disabled: boolean;
}) {
  const [search, setSearch] = useState("");
  const options = useResource<Page<{ id: string; name: string }>>(
    `applications/resume-options?search=${encodeURIComponent(search)}`,
  );
  return (
    <div className="ai-resume">
      <label>
        Find a saved resume
        <input
          value={search}
          maxLength={100}
          disabled={disabled}
          onChange={(e) => {
            setSearch(e.target.value);
            change("");
          }}
        />
      </label>
      <label>
        Selected resume
        <select
          value={value}
          disabled={disabled || !options?.data}
          onChange={(e) => change(e.target.value)}
        >
          <option value="">Choose a resume</option>
          {options?.data?.items.map((x) => (
            <option key={x.id} value={x.id}>
              {x.name}
            </option>
          ))}
        </select>
      </label>
      {!options && <p role="status">Loading saved resumes…</p>}
      {options?.error && <p role="alert">{options.error}</p>}
      {options?.data?.items.length === 0 && (
        <p>No matching saved resumes. Add a resume or refine the search.</p>
      )}
      {options?.data && (
        <p>
          Showing up to 20 matches. Refine the search to find another resume.
          Only explicitly recorded resume skills are read; PDF content is never
          read by AI. <Link href="/resumes">Manage resume evidence</Link>.
        </p>
      )}
    </div>
  );
}
export function AssistancePage({ jobId }: { jobId: string }) {
  const [attempt, retry] = useState(0);
  const job = useResource<Job>(`jobs/${jobId}`, attempt);
  const status = useResource<Availability>("ai/status", attempt);
  const [mode, setMode] = useState("job"),
    [resumeId, setResume] = useState(""),
    [consent, setConsent] = useState(false),
    [busy, setBusy] = useState(false),
    [error, setError] = useState("");
  const [result, setResult] = useState<Assistance>();
  const active = useRef<AbortController | null>(null);
  useEffect(() => () => active.current?.abort(), []);
  function clear() {
    active.current?.abort();
    setBusy(false);
    setResult(undefined);
    setError("");
    setConsent(false);
  }
  async function generate(e: React.FormEvent) {
    e.preventDefault();
    if (busy || !consent || status?.data?.state !== "Ready") return;
    const controller = new AbortController();
    active.current = controller;
    setBusy(true);
    setError("");
    setResult(undefined);
    try {
      const value = await request<Assistance>(
        `jobs/${jobId}/assistance`,
        "POST",
        { mode, resumeId: mode === "resume" ? resumeId : null, consent: true },
        controller.signal,
      );
      if (!controller.signal.aborted) setResult(value);
    } catch (e) {
      if (!controller.signal.aborted)
        setError(e instanceof ApiError ? e.message : failure(e));
    } finally {
      if (!controller.signal.aborted) setBusy(false);
    }
  }
  const ready = status?.data?.state === "Ready";
  const description = job?.data?.description;
  return (
    <div className="editor-page ai-page">
      <Link className="back-link" href={`/jobs/${jobId}`}>
        ← Job details & deterministic matching
      </Link>
      <header className="ai-heading">
        <p className="eyebrow">Optional · local only</p>
        <h1>Prepare with more clarity</h1>
        <p>
          AI assistance for {job?.data?.title ?? "your saved opportunity"}. Your
          records stay in your local environment.
        </p>
      </header>
      <div className="ai-status">
        <strong>
          {status?.data?.state === "Ready"
            ? "Local model available"
            : status?.data?.state === "Disabled"
              ? "Local AI is off"
              : "Local AI setup"}
        </strong>
        {!status && <p role="status">Checking local AI configuration…</p>}
        {status?.data && <p>{status.data.message}</p>}
        {status?.error && <p role="alert">{status.error}</p>}
        <button
          type="button"
          className="secondary-button"
          disabled={busy}
          onClick={() => retry(attempt + 1)}
        >
          Check again
        </button>
        <p>
          Deterministic matching, Skills Intelligence and all core CareerOS
          features work without AI. No subscription or model download is
          required.
        </p>
        <div className="ai-links">
          <Link href={`/jobs/${jobId}`}>Job matching</Link>
          <Link href="/skills">Skills Intelligence</Link>
        </div>
      </div>
      {!job && <p role="status">Loading saved job…</p>}
      {job?.error && <p role="alert">{job.error}</p>}
      <details className="ai-setup">
        <summary>Optional local setup & privacy</summary>
        <p>
          An administrator can install Ollama and manually choose a small local
          model after checking its license and memory needs. CareerOS never
          installs Ollama or downloads models.
        </p>
        <ol>
          <li>
            Run Ollama on this API machine at 127.0.0.1:11434 with cloud
            features disabled (OLLAMA_NO_CLOUD=1).
          </li>
          <li>
            Use a server version that reports cloud-disabled status and local
            GGUF model details. Older or unverifiable servers are unavailable.
          </li>
          <li>
            Configure LocalAi:Model with the installed model’s explicit tag and
            set LocalAi:Enabled to true in API configuration; restart the API.
          </li>
        </ol>
        <p>
          CPU inference can be slow; large models may not fit available memory.
          Leave AI off if your hardware is limited. No cloud fallback, web
          search or tool execution is allowed.
        </p>
        <p>
          Only a saved job description is used for job/interview help. Resume
          help also uses recorded profile headline, summary, experience years
          and explicit profile/selected-resume skills. Account name/email
          fields, file paths, PDF bytes and other resumes are excluded. Saved
          text may itself contain personal data; review it before opting in.
          Output is temporary, never automatically saved or applied. The local
          inference daemon and machine administrator must be trusted.
        </p>
      </details>
      <form onSubmit={generate} className="ai-form">
        <fieldset>
          <legend>What would you like to prepare?</legend>
          <div className="ai-modes">
            {[
              ["job", "Job analysis"],
              ["resume", "Resume improvement"],
              ["interview", "Interview practice"],
            ].map(([value, label]) => (
              <label key={value}>
                <input
                  type="radio"
                  name="mode"
                  checked={mode === value}
                  onChange={() => {
                    clear();
                    setMode(value!);
                    setResume("");
                  }}
                />
                {label}
              </label>
            ))}
          </div>
        </fieldset>
        <p>
          {mode === "job"
            ? "Select concise responsibility and qualification quotes from the saved description, with preparation suggestions."
            : mode === "resume"
              ? "Prioritize improvements using only your recorded information. No skills, achievements or experience will be invented."
              : "Practice relevant questions grounded in exact job-description excerpts. No answer is assumed about your experience."}
        </p>
        {mode === "resume" && (
          <ResumeChoice
            value={resumeId}
            change={(v) => {
              clear();
              setResume(v);
            }}
            disabled={busy}
          />
        )}
        {job?.data && !description?.trim() && (
          <p className="ai-notice">
            Add a saved job description first.{" "}
            <Link href={`/jobs/${jobId}/edit`}>Edit this job</Link>.
          </p>
        )}
        {description && description.length > 10000 && (
          <p className="ai-notice">
            This description exceeds the 10,000-character assistance limit. It
            will not be silently truncated.
          </p>
        )}
        <label className="ai-consent">
          <input
            type="checkbox"
            checked={consent}
            disabled={!ready || busy}
            onChange={(e) => setConsent(e.target.checked)}
          />
          I choose to process this saved context with the local model. I will
          verify every suggestion before using it.
        </label>
        <div className="ai-actions">
          <button
            className="primary-button"
            disabled={
              !ready ||
              !job?.data ||
              !description?.trim() ||
              description.length > 10000 ||
              !consent ||
              busy ||
              (mode === "resume" && !resumeId)
            }
          >
            {busy ? "Preparing locally…" : "Generate local suggestions"}
          </button>
          {busy && (
            <button
              type="button"
              className="secondary-button"
              onClick={() => {
                active.current?.abort();
                setBusy(false);
                setError("Request cancelled. No suggestions were saved.");
              }}
            >
              Cancel
            </button>
          )}
        </div>
        <p className="field-hint">
          Up to 90 seconds · one generation at a time · no conversation history
          retained by CareerOS
        </p>
        {busy && (
          <p role="status">
            The local model is preparing suggestions. You can cancel or keep
            using other CareerOS tools.
          </p>
        )}
        {error && <p role="alert">{error}</p>}
      </form>
      {result && (
        <section className="ai-output" aria-label="AI assistance result">
          <header>
            <p className="eyebrow">{result.label}</p>
            <h2>Review before you use</h2>
            <p>
              {result.model} ·{" "}
              {new Date(result.generatedAtUtc).toLocaleString()}
            </p>
          </header>
          <p>
            AI selected these excerpts and preparation actions. Job quotes are
            untrusted employer text, not verified candidate facts.
            Recommendations never update your profile, resume, matching scores
            or applications.
          </p>
          {mode === "job" && (
            <>
              <ResultList
                title="Responsibilities · exact job quotes"
                items={result.responsibilities}
              />
              <ResultList
                title="Qualifications · exact job quotes"
                items={result.qualifications}
              />
            </>
          )}
          {mode === "interview" && (
            <ResultList
              title="Practice questions · suggestions"
              items={result.questions}
            />
          )}
          <ResultList
            title="Preparation & improvement suggestions"
            items={result.suggestions}
          />
          {mode === "resume" && (
            <section className="ai-section">
              <h3>Recorded facts used · user supplied</h3>
              <p>
                These are copied from your saved records, not independently
                verified. Profile skills and selected-resume skills remain
                distinct.
              </p>
              <dl>
                {result.facts.map((x) => (
                  <div key={x.id}>
                    <dt>{x.source}</dt>
                    <dd>{x.value}</dd>
                  </div>
                ))}
              </dl>
            </section>
          )}
        </section>
      )}
    </div>
  );
}
