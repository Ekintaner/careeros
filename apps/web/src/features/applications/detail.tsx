"use client";
import { useState, type FormEvent } from "react";
import Link from "next/link";
import { ApiError } from "@/lib/api-client";
import { Feedback, Field, useUnsavedChanges } from "@/features/profile/form-ui";
import { useResource } from "@/features/opportunities/lists";
import { Pagination } from "@/features/opportunities/shared";
import {
  request,
  failure,
  date,
  safeUrl,
  type ApplicationView,
  type Page,
  type History,
  type Note,
} from "./client";
import { StatusControl } from "./status-control";

function Notes({ id }: { id: string }) {
  const [page, setPage] = useState(1);
  const [attempt, setAttempt] = useState(0);
  const [draft, setDraft] = useState("");
  const [editing, setEditing] = useState<Note>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [conflict, setConflict] = useState(false);
  const resource = useResource<Page<Note>>(
    `applications/${id}/notes?page=${page}`,
    attempt,
  );
  useUnsavedChanges(editing ? draft !== editing.body : !!draft);
  async function save(e: FormEvent) {
    e.preventDefault();
    if (busy || !draft.trim()) return;
    setBusy(true);
    setError("");
    setSuccess("");
    try {
      await request(
        `applications/${id}/notes${editing ? `/${editing.id}` : ""}`,
        editing ? "PUT" : "POST",
        { body: draft.trim(), version: editing?.version ?? 0 },
      );
      setDraft("");
      setEditing(undefined);
      setSuccess(editing ? "Note updated." : "Note added.");
      setAttempt((n) => n + 1);
      setPage(1);
    } catch (e) {
      setError(failure(e));
      setConflict(e instanceof ApiError && e.status === 409);
    } finally {
      setBusy(false);
    }
  }
  async function remove(note: Note) {
    if (busy || !window.confirm("Delete this note? This cannot be undone."))
      return;
    setBusy(true);
    setError("");
    setSuccess("");
    try {
      await request(
        `applications/${id}/notes/${note.id}?version=${note.version}`,
        "DELETE",
      );
      setAttempt((n) => n + 1);
      setPage(1);
      setSuccess("Note deleted.");
    } catch (e) {
      setError(failure(e));
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="application-notes">
      <h2>Notes</h2>
      <p className="field-hint">
        Private context for this application. Plain text only.
      </p>
      <Feedback error={error || resource?.error} success={success} />
      <form onSubmit={save}>
        <Field id="note-body" title={editing ? "Edit note" : "New note"}>
          <textarea
            id="note-body"
            rows={4}
            maxLength={20000}
            value={draft}
            disabled={busy}
            onChange={(e) => setDraft(e.target.value)}
          />
        </Field>
        <div className="detail-actions">
          <button
            className="primary-button"
            disabled={busy || !draft.trim() || conflict}
          >
            {busy ? "Saving…" : editing ? "Save note" : "Add note"}
          </button>
          {editing && (
            <button
              className="secondary-button"
              type="button"
              disabled={busy}
              onClick={() => {
                if (
                  draft === editing.body ||
                  window.confirm("Discard note changes?")
                ) {
                  setEditing(undefined);
                  setDraft("");
                  setConflict(false);
                }
              }}
            >
              Cancel edit
            </button>
          )}
        </div>
      </form>
      {(conflict || resource?.error || error) && (
        <button
          className="text-button"
          disabled={busy}
          onClick={() => {
            if (
              !draft ||
              draft === editing?.body ||
              window.confirm("Discard your note draft and reload?")
            ) {
              setEditing(undefined);
              setDraft("");
              setConflict(false);
              setError("");
              setAttempt((n) => n + 1);
            }
          }}
        >
          Reload notes
        </button>
      )}
      {!resource ? (
        <p role="status">Loading notes…</p>
      ) : !resource.error && !resource.data?.items.length ? (
        <p className="stage-empty">No notes yet.</p>
      ) : (
        resource.data?.items.map((note) => (
          <article key={note.id} className="application-note">
            <p>{note.body}</p>
            <footer>
              <span>
                {date(note.createdAtUtc)}
                {note.updatedAtUtc !== note.createdAtUtc ? " · Edited" : ""}
              </span>
              <div>
                <button
                  className="text-button"
                  disabled={busy || !!editing}
                  onClick={() => {
                    if (
                      !draft ||
                      window.confirm("Discard your new note draft?")
                    ) {
                      setEditing(note);
                      setDraft(note.body);
                      setError("");
                      setConflict(false);
                      document.getElementById("note-body")?.focus();
                    }
                  }}
                >
                  Edit note
                </button>
                <button
                  className="text-button danger-text"
                  disabled={busy || !!editing}
                  onClick={() => void remove(note)}
                >
                  Delete note
                </button>
              </div>
            </footer>
          </article>
        ))
      )}
      {resource?.data && resource.data.totalPages > 1 && (
        <Pagination
          page={page}
          pages={resource.data.totalPages}
          count={resource.data.totalCount}
          onPage={setPage}
        />
      )}
    </section>
  );
}
function Timeline({ id, attempt }: { id: string; attempt: number }) {
  const [page, setPage] = useState(1);
  const resource = useResource<Page<History>>(
    `applications/${id}/history?page=${page}`,
    attempt,
  );
  return (
    <section className="application-history">
      <h2>Status history</h2>
      <p className="field-hint">
        Every recorded stage, in order. Times shown in UTC.
      </p>
      <Feedback error={resource?.error} />
      {!resource ? (
        <p role="status">Loading history…</p>
      ) : (
        <ol className="history-timeline">
          {resource.data?.items.map((h) => (
            <li key={h.id}>
              <strong>{h.toStatus}</strong>
              <p>
                {h.fromStatus
                  ? `${h.fromStatus} → ${h.toStatus}`
                  : "Initial status"}
              </p>
              <time dateTime={h.changedAtUtc}>
                {date(h.changedAtUtc)} ·{" "}
                {new Date(h.changedAtUtc).toISOString().slice(11, 16)} UTC
              </time>
            </li>
          ))}
        </ol>
      )}
      {resource?.data && resource.data.totalPages > 1 && (
        <Pagination
          page={page}
          pages={resource.data.totalPages}
          count={resource.data.totalCount}
          onPage={setPage}
        />
      )}
    </section>
  );
}
export function ApplicationDetail({ id }: { id: string }) {
  const [attempt, setAttempt] = useState(0);
  const resource = useResource<ApplicationView>(`applications/${id}`, attempt);
  if (resource?.data)
    return <LoadedDetail key={id} id={id} initial={resource.data} />;
  return (
    <div className="editor-page">
      <Link className="back-link" href="/applications">
        ← Applications
      </Link>
      {!resource ? (
        <p role="status">Loading application…</p>
      ) : (
        <Feedback error={resource.error} />
      )}
      <button className="text-button" onClick={() => setAttempt((n) => n + 1)}>
        Reload latest application
      </button>
    </div>
  );
}
function LoadedDetail({
  id,
  initial,
}: {
  id: string;
  initial: ApplicationView;
}) {
  const [attempt, setAttempt] = useState(0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [conflict, setConflict] = useState(false);
  const [a, setApplication] = useState(initial);
  async function reload() {
    setBusy(true);
    setError("");
    try {
      setApplication(await request<ApplicationView>(`applications/${id}`));
      setConflict(false);
      setAttempt((n) => n + 1);
    } catch (e) {
      setError(failure(e));
      setConflict(true);
    } finally {
      setBusy(false);
    }
  }
  async function archive() {
    if (!a || busy) return;
    setBusy(true);
    setError("");
    try {
      const updated = await request<ApplicationView>(
        `applications/${id}/${a.status === "Archived" ? "restore" : "archive"}`,
        "POST",
        { version: a.version },
      );
      setSuccess(
        a.status === "Archived"
          ? "Application restored to its previous status."
          : "Application archived. Its history is preserved.",
      );
      setApplication(updated);
      setAttempt((n) => n + 1);
    } catch (e) {
      setError(failure(e));
      setConflict(e instanceof ApiError && e.status === 409);
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="editor-page">
      <Link className="back-link" href="/applications">
        ← Applications
      </Link>
      <Feedback error={error} success={success} />
      {a && (
        <>
          <header className="page-heading job-heading">
            <div>
              <p className="eyebrow">{a.companyName}</p>
              <h1>{a.jobTitle}</h1>
              <p>{a.location}</p>
            </div>
            <span className="application-status">{a.status}</span>
          </header>
          <div className="detail-actions">
            <Link href={`/interviews?applicationId=${a.id}`}>Interviews</Link>
            <Link href={`/interviews/new?applicationId=${a.id}`}>
              Schedule interview
            </Link>
            <Link href={`/tasks?applicationId=${a.id}`}>Follow-up tasks</Link>
            <Link href={`/tasks/new?applicationId=${a.id}`}>
              New follow-up task
            </Link>
          </div>
          <div className="detail-actions">
            <Link className="primary-button" href={`/applications/${id}/edit`}>
              Edit application
            </Link>
            <button
              className="secondary-button"
              disabled={busy || conflict}
              onClick={() => void archive()}
            >
              {busy
                ? "Saving…"
                : a.status === "Archived"
                  ? "Restore application"
                  : "Archive application"}
            </button>
            <Link className="secondary-button" href={`/jobs/${a.jobId}`}>
              View job
            </Link>
            {safeUrl(a.sourceUrl) && (
              <a
                className="secondary-button"
                href={safeUrl(a.sourceUrl)}
                target="_blank"
                rel="noopener noreferrer"
              >
                Original posting ↗
              </a>
            )}
          </div>
          <dl className="job-facts">
            {[
              [
                "Applied",
                a.appliedAtUtc ? date(a.appliedAtUtc) : "Not applied yet",
              ],
              ["Source", a.source ?? "Not specified"],
              ["Resume", a.resumeName ?? "No resume assigned"],
              ["Created", date(a.createdAtUtc)],
              ["Updated", date(a.updatedAtUtc)],
            ].map(([title, value]) => (
              <div key={title}>
                <dt>{title}</dt>
                <dd>{value}</dd>
              </div>
            ))}
          </dl>
          <div className="detail-status">
            <StatusControl
              key={a.version}
              application={a}
              onChanged={() => {
                setSuccess("Status updated. History recorded.");
                void reload();
              }}
            />
          </div>
          <div className="application-detail-grid">
            <Notes id={id} />
            <Timeline id={id} attempt={attempt} />
          </div>
        </>
      )}
      <button
        className="text-button"
        disabled={busy}
        onClick={() => {
          void reload();
        }}
      >
        Reload latest application
      </button>
    </div>
  );
}
