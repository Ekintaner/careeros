"use client";
import { useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Feedback, Field, useUnsavedChanges } from "@/features/profile/form-ui";
import { useResource } from "@/features/opportunities/lists";
import { Pagination } from "@/features/opportunities/shared";
import {
  request,
  failure,
  safeUrl,
  type Page,
  type ApplicationCard,
} from "@/features/applications/client";
import {
  interviewTypes,
  inputTime,
  utcTime,
  timestamp,
  type Interview,
  type CareerTask,
} from "./types";
import { InterviewNotes } from "./notes";

function ApplicationPicker({
  value,
  change,
  optional,
  disabled,
}: {
  value: string;
  change: (id: string) => void;
  optional: boolean;
  disabled: boolean;
}) {
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [attempt, setAttempt] = useState(0);
  const data = useResource<Page<ApplicationCard>>(
    `applications?state=all&search=${encodeURIComponent(search)}&page=${page}`,
    attempt,
  );
  return (
    <section aria-label="Choose application">
      <Field id="application-search" title="Find an application">
        <input
          id="application-search"
          maxLength={200}
          value={search}
          disabled={disabled}
          onChange={(e) => {
            setSearch(e.target.value);
            setPage(1);
          }}
        />
      </Field>
      <Field
        id="application"
        title={optional ? "Application (optional)" : "Application"}
      >
        <select
          id="application"
          value={value}
          disabled={disabled}
          required={!optional}
          onChange={(e) => change(e.target.value)}
        >
          <option value="">
            {optional ? "No linked application" : "Choose an application"}
          </option>
          {value && !data?.data?.items.some((x) => x.id === value) && (
            <option value={value}>Selected application</option>
          )}
          {data?.data?.items.map((x) => (
            <option key={x.id} value={x.id}>
              {x.companyName} · {x.jobTitle} ({x.status})
            </option>
          ))}
        </select>
      </Field>
      {!data && <p role="status">Loading applications…</p>}
      {data?.error && (
        <>
          <Feedback error={data.error} />
          <button type="button" onClick={() => setAttempt((n) => n + 1)}>
            Retry applications
          </button>
        </>
      )}
      {data?.data && (
        <Pagination
          page={page}
          pages={data.data.totalPages}
          count={data.data.totalCount}
          busy={disabled}
          onPage={setPage}
        />
      )}
      <Link href="/applications">Manage applications</Link>
    </section>
  );
}

export function PlanningEditor({
  tasks = false,
  id,
  applicationId = "",
}: {
  tasks?: boolean;
  id?: string;
  applicationId?: string;
}) {
  const [attempt, setAttempt] = useState(0);
  const root = tasks ? "tasks" : "interviews";
  const data = useResource<CareerTask | Interview>(
    id ? `${root}/${id}` : "auth/me",
    attempt,
  );
  return (
    <section className="planning-page">
      <Link href={`/${root}`}>← {tasks ? "Tasks" : "Interviews"}</Link>
      <h1>
        {id ? "Details" : tasks ? "New career task" : "Schedule interview"}
      </h1>
      {!data ? (
        <p role="status">Loading…</p>
      ) : data?.error ? (
        <>
          <Feedback error={data.error} />
          <button onClick={() => setAttempt((n) => n + 1)}>Retry</button>
        </>
      ) : (
        <EditorForm
          key={`${id ?? "new"}:${attempt}`}
          tasks={tasks}
          existing={id ? data?.data : undefined}
          applicationId={applicationId}
          reload={() => setAttempt((n) => n + 1)}
        />
      )}
    </section>
  );
}
function EditorForm({
  tasks,
  existing,
  applicationId,
  reload,
}: {
  tasks: boolean;
  existing?: CareerTask | Interview;
  applicationId: string;
  reload: () => void;
}) {
  const router = useRouter();
  const root = tasks ? "tasks" : "interviews";
  const [snapshot, setSnapshot] = useState(existing);
  const [basis, setBasis] = useState("local");
  const [application, setApplication] = useState(
    existing?.applicationId ?? applicationId,
  );
  const [title, setTitle] = useState(
    existing && "title" in existing ? existing.title : "",
  );
  const [description, setDescription] = useState(
    existing && "description" in existing ? (existing.description ?? "") : "",
  );
  const [priority, setPriority] = useState(
    existing && "priority" in existing ? existing.priority : "Normal",
  );
  const [completed, setCompleted] = useState(
    !!(existing && "completedAtUtc" in existing && existing.completedAtUtc),
  );
  const [type, setType] = useState(
    existing && "type" in existing ? existing.type : "Technical",
  );
  const [start, setStart] = useState(
    inputTime(
      existing && "scheduledStartUtc" in existing
        ? existing.scheduledStartUtc
        : null,
      "local",
    ),
  );
  const [end, setEnd] = useState(
    inputTime(
      existing && "scheduledEndUtc" in existing
        ? existing.scheduledEndUtc
        : null,
      "local",
    ),
  );
  const [due, setDue] = useState(
    inputTime(
      existing && "dueAtUtc" in existing ? existing.dueAtUtc : null,
      "local",
    ),
  );
  const [location, setLocation] = useState(
    existing && "location" in existing ? (existing.location ?? "") : "",
  );
  const [url, setUrl] = useState(
    existing && "meetingUrl" in existing ? (existing.meetingUrl ?? "") : "",
  );
  const [outcome, setOutcome] = useState(
    existing && "outcome" in existing ? (existing.outcome ?? "") : "",
  );
  const [busy, setBusy] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  useUnsavedChanges(dirty);
  const deviceZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
  function changeBasis(next: string) {
    try {
      const values = [start, end, due].map((x) => utcTime(x, basis));
      setStart(inputTime(values[0], next));
      setEnd(inputTime(values[1], next));
      setDue(inputTime(values[2], next));
      setBasis(next);
      setError("");
    } catch {
      // Keep wall-clock values so ambiguous local times can be explicitly re-entered as UTC.
      setBasis(next);
      setStart("");
      setEnd("");
      setDue("");
      setError("Re-enter the exact time in the selected timezone.");
    }
  }
  async function save(e: FormEvent) {
    e.preventDefault();
    if (busy) return;
    setBusy(true);
    setError("");
    setSuccess("");
    try {
      const startUtc = utcTime(start, basis),
        endUtc = utcTime(end, basis);
      if (!tasks && (!startUtc || !endUtc || endUtc <= startUtc))
        throw new Error("Interview end must be after start.");
      if (url && !safeUrl(url))
        throw new Error("Use an HTTP(S) meeting URL without credentials.");
      const input = tasks
        ? {
            title,
            description,
            priority,
            completed,
            applicationId: application || null,
            dueAtUtc: utcTime(due, basis),
            version: snapshot?.version ?? 0,
          }
        : {
            applicationId: application,
            type,
            scheduledStartUtc: startUtc,
            scheduledEndUtc: endUtc,
            location,
            meetingUrl: url,
            outcome,
            version: snapshot?.version ?? 0,
          };
      const saved = await request<CareerTask | Interview>(
        `${root}${snapshot ? `/${snapshot.id}` : ""}`,
        snapshot ? "PUT" : "POST",
        input,
      );
      setSnapshot(saved);
      setDirty(false);
      setSuccess("Saved.");
      if (!snapshot) router.replace(`/${root}/${saved.id}`);
    } catch (e) {
      setError(e instanceof Error && !("status" in e) ? e.message : failure(e));
    } finally {
      setBusy(false);
    }
  }
  async function remove() {
    if (
      !snapshot ||
      busy ||
      !window.confirm(
        `Delete this ${tasks ? "task" : "interview"}? This cannot be undone.`,
      )
    )
      return;
    setBusy(true);
    setError("");
    try {
      await request(
        `${root}/${snapshot.id}?version=${snapshot.version}`,
        "DELETE",
      );
      setDirty(false);
      router.push(`/${root}`);
    } catch (e) {
      setError(failure(e));
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      <Feedback error={error} success={success} />
      <p className="field-hint">
        Displayed times: {deviceZone}. Saved as UTC. No calendar invitations are
        sent.
      </p>
      <form
        className="planning-form"
        onSubmit={save}
        onChange={() => setDirty(true)}
      >
        <fieldset disabled={busy}>
          <legend>{tasks ? "Task details" : "Interview details"}</legend>
          {(!snapshot || tasks) && (
            <ApplicationPicker
              value={application}
              change={setApplication}
              optional={tasks}
              disabled={busy}
            />
          )}
          {application && (
            <p>
              <Link href={`/applications/${application}`}>
                View linked application
              </Link>
            </p>
          )}
          <Field id="time-basis" title="Enter times in">
            <select
              id="time-basis"
              value={basis}
              onChange={(e) => changeBasis(e.target.value)}
            >
              <option value="local">Device timezone ({deviceZone})</option>
              <option value="UTC">UTC</option>
            </select>
          </Field>
          {tasks ? (
            <>
              <Field id="task-title" title="Title">
                <input
                  id="task-title"
                  required
                  maxLength={200}
                  value={title}
                  onChange={(e) => setTitle(e.target.value)}
                />
              </Field>
              <Field id="task-description" title="Description">
                <textarea
                  id="task-description"
                  rows={5}
                  maxLength={10000}
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                />
              </Field>
              <Field id="task-due" title="Due date and time (optional)">
                <input
                  id="task-due"
                  type="datetime-local"
                  value={due}
                  onChange={(e) => setDue(e.target.value)}
                />
              </Field>
              <Field id="task-priority" title="Priority">
                <select
                  id="task-priority"
                  value={priority}
                  onChange={(e) => setPriority(e.target.value)}
                >
                  {["Low", "Normal", "High"].map((p) => (
                    <option key={p}>{p}</option>
                  ))}
                </select>
              </Field>
              <label className="planning-check">
                <input
                  type="checkbox"
                  checked={completed}
                  onChange={(e) => setCompleted(e.target.checked)}
                />{" "}
                Completed
              </label>
            </>
          ) : (
            <>
              <Field id="interview-type" title="Interview type">
                <select
                  id="interview-type"
                  value={type}
                  onChange={(e) => setType(e.target.value)}
                >
                  {interviewTypes.map((x) => (
                    <option key={x}>{x}</option>
                  ))}
                </select>
              </Field>
              <div className="planning-grid">
                <Field id="interview-start" title="Start">
                  <input
                    id="interview-start"
                    type="datetime-local"
                    required
                    value={start}
                    onChange={(e) => setStart(e.target.value)}
                  />
                </Field>
                <Field id="interview-end" title="End">
                  <input
                    id="interview-end"
                    type="datetime-local"
                    required
                    value={end}
                    onChange={(e) => setEnd(e.target.value)}
                  />
                </Field>
              </div>
              <Field id="interview-location" title="Location">
                <input
                  id="interview-location"
                  maxLength={300}
                  value={location}
                  onChange={(e) => setLocation(e.target.value)}
                />
              </Field>
              <Field id="meeting-url" title="Meeting URL">
                <input
                  id="meeting-url"
                  type="url"
                  maxLength={2048}
                  value={url}
                  onChange={(e) => setUrl(e.target.value)}
                />
              </Field>
              {safeUrl(url) && (
                <p>
                  <a
                    href={safeUrl(url)}
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    Open meeting link
                  </a>
                </p>
              )}
              <Field id="interview-outcome" title="Outcome">
                <textarea
                  id="interview-outcome"
                  rows={4}
                  maxLength={2000}
                  value={outcome}
                  onChange={(e) => setOutcome(e.target.value)}
                />
              </Field>
            </>
          )}
          <div className="detail-actions">
            <button className="primary-button" disabled={busy}>
              {busy ? "Saving…" : "Save"}
            </button>
            {snapshot && (
              <button
                type="button"
                className="secondary-button danger-text"
                disabled={busy}
                onClick={() => void remove()}
              >
                Delete
              </button>
            )}
          </div>
        </fieldset>
      </form>
      {snapshot && (
        <>
          <p className="field-hint">
            Last updated {timestamp(snapshot.updatedAtUtc)}
          </p>
          <button
            className="text-button"
            disabled={busy}
            onClick={() => {
              if (
                !dirty ||
                window.confirm("Discard unsaved changes and reload?")
              )
                reload();
            }}
          >
            Reload latest
          </button>
        </>
      )}
      {!tasks && snapshot && <InterviewNotes id={snapshot.id} />}
    </>
  );
}
