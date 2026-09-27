"use client";
import { useId, useState } from "react";
import { ApiError } from "@/lib/api-client";
import { Feedback } from "@/features/profile/form-ui";
import { request, failure, nextStatuses, type ApplicationCard } from "./client";

export function StatusControl({
  application: a,
  onChanged,
}: {
  application: ApplicationCard;
  onChanged: () => void;
}) {
  const id = useId();
  const [next, setNext] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [conflict, setConflict] = useState(false);
  const options = nextStatuses(a.status);
  if (!options.length) return null;
  async function submit() {
    if (!next || busy) return;
    setBusy(true);
    setError("");
    try {
      await request(`applications/${a.id}/status`, "POST", {
        status: next,
        version: a.version,
      });
      setNext("");
      onChanged();
    } catch (e) {
      setError(failure(e));
      setConflict(e instanceof ApiError && e.status === 409);
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="status-control">
      <label htmlFor={id}>Move to</label>
      <div className="status-inputs">
        <select
          aria-label={`Move ${a.jobTitle} to`}
          id={id}
          value={next}
          disabled={busy || conflict}
          onChange={(e) => setNext(e.target.value)}
        >
          <option value="">Choose stage</option>
          {options.map((s) => (
            <option key={s}>{s}</option>
          ))}
        </select>
        <button
          className="secondary-button"
          disabled={!next || busy || conflict}
          onClick={() => void submit()}
        >
          {busy ? "Moving…" : "Move"}
        </button>
      </div>
      <Feedback error={error} />
      {conflict && (
        <button className="text-button" onClick={onChanged}>
          Reload current stage
        </button>
      )}
    </div>
  );
}
