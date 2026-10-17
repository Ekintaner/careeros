"use client";
import { useState, type FormEvent } from "react";
import { ApiError } from "@/lib/api-client";
import { Feedback, Field, useUnsavedChanges } from "@/features/profile/form-ui";
import { useResource } from "@/features/opportunities/lists";
import { Pagination } from "@/features/opportunities/shared";
import {
  request,
  failure,
  type Page,
  type Note,
} from "@/features/applications/client";
import { timestamp } from "./types";
export function InterviewNotes({ id }: { id: string }) {
  const [page, setPage] = useState(1);
  const [attempt, setAttempt] = useState(0);
  const [draft, setDraft] = useState("");
  const [editing, setEditing] = useState<Note>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [conflict, setConflict] = useState(false);
  const resource = useResource<Page<Note>>(
    `interviews/${id}/notes?page=${page}`,
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
        `interviews/${id}/notes${editing ? `/${editing.id}` : ""}`,
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
        `interviews/${id}/notes/${note.id}?version=${note.version}`,
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
        Private context for this interview. Plain text only.
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
                {timestamp(note.createdAtUtc)}
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
