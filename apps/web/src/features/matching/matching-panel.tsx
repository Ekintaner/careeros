"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { request, failure, date } from "@/features/opportunities/client";
import { Feedback, useUnsavedChanges } from "@/features/profile/form-ui";
import type { Matching } from "./types";

function names(text: string) {
  return text
    .split(/\r?\n/)
    .map((s) => s.trim())
    .filter(Boolean);
}
function validate(values: string[]) {
  if (values.length > 100) return "Use at most 100 skills.";
  if (values.some((s) => s.length > 100 || /[^\x20-\x7e]/.test(s)))
    return "Each skill must contain 1–100 printable ASCII characters.";
  if (new Set(values.map((s) => s.toLowerCase())).size !== values.length)
    return "Remove duplicate skills (including between Required and Preferred).";
  return "";
}
function SkillEditor({
  data,
  kind,
  close,
  saved,
}: {
  data: Matching;
  kind: "job" | "resume";
  close: () => void;
  saved: () => void;
}) {
  const resume = kind === "resume";
  const original = resume
    ? data.resume!.skills.map((s) => s.name).join("\n")
    : data.result.skills
        .filter((s) => s.requirementLevel === "Required")
        .map((s) => s.name)
        .join("\n");
  const preferred = data.result.skills
    .filter((s) => s.requirementLevel === "Preferred")
    .map((s) => s.name)
    .join("\n");
  const [first, setFirst] = useState(original);
  const [second, setSecond] = useState(preferred);
  const [confirmed, setConfirmed] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [conflict, setConflict] = useState(false);
  const dirty = first !== original || (!resume && second !== preferred);
  useUnsavedChanges(dirty);
  async function save(event: React.FormEvent) {
    event.preventDefault();
    const one = names(first),
      two = resume ? [] : names(second);
    const invalid = validate([...one, ...two]);
    if (invalid) {
      setError(invalid);
      return;
    }
    if (resume && !confirmed) {
      setError("Confirm that every listed skill is evidenced in this resume.");
      return;
    }
    setBusy(true);
    setError("");
    try {
      await request(
        resume
          ? `resumes/${data.resume!.id}/skills`
          : `jobs/${data.jobId}/skills`,
        "PUT",
        resume
          ? { version: data.resume!.version, names: one }
          : {
              version: data.jobVersion,
              skills: [
                ...one.map((name) => ({ name, requirementLevel: "Required" })),
                ...two.map((name) => ({ name, requirementLevel: "Preferred" })),
              ],
            },
      );
      saved();
    } catch (e) {
      setError(failure(e));
      // A stale editor must reload explicitly; preserve typed values for review/copy.
      if (
        typeof e === "object" &&
        e !== null &&
        "status" in e &&
        e.status === 409
      )
        setConflict(true);
    } finally {
      setBusy(false);
    }
  }
  return (
    <form className="editor-field" onSubmit={(e) => void save(e)}>
      <h3>
        {resume
          ? `Confirm skills in ${data.resume!.name}`
          : "Job skill requirements"}
      </h3>
      <p className="field-hint">
        One skill per line, at most 100 total. Use exact names consistently, for
        example C#, C++, .NET, React. Skill names are shared vocabulary; keep
        personal information out.
      </p>
      <p>
        {resume
          ? "Only list skills supported by this exact PDF. This records your confirmation; the file is not parsed and profile skills are not copied automatically."
          : "Copy requirements from the posting and check their importance. This does not add skills to your profile or resume."}
      </p>
      <label htmlFor="match-first">
        {resume ? "Skills evidenced in this resume" : "Required skills"}
      </label>
      <textarea
        id="match-first"
        rows={6}
        maxLength={10100}
        value={first}
        onChange={(e) => setFirst(e.target.value)}
        disabled={busy}
      />
      {!resume && (
        <>
          <label htmlFor="match-preferred">Preferred skills</label>
          <textarea
            id="match-preferred"
            rows={4}
            maxLength={10100}
            value={second}
            onChange={(e) => setSecond(e.target.value)}
            disabled={busy}
          />
        </>
      )}
      {resume && (
        <label>
          <input
            type="checkbox"
            checked={confirmed}
            onChange={(e) => setConfirmed(e.target.checked)}
            disabled={busy}
          />{" "}
          I checked this PDF and confirm every listed skill is evidenced in it.
        </label>
      )}
      <Feedback error={error} />
      <div className="detail-actions">
        <button className="primary-button" disabled={busy || conflict}>
          {busy ? "Saving…" : "Save skills"}
        </button>
        <button
          className="secondary-button"
          type="button"
          disabled={busy}
          onClick={() => {
            if (!dirty || window.confirm("Discard unsaved skill changes?"))
              close();
          }}
        >
          {conflict ? "Discard and reload latest skills" : "Cancel"}
        </button>
      </div>
    </form>
  );
}

export function MatchingPanel({ jobId }: { jobId: string }) {
  const [resumeId, setResumeId] = useState("");
  const [page, setPage] = useState(1);
  const [files, setFiles] = useState<{ resumeId: string; name: string }[]>([]);
  const [total, setTotal] = useState(0);
  const [fileError, setFileError] = useState("");
  const [data, setData] = useState<Matching | null>(null);
  const [error, setError] = useState("");
  const [attempt, setAttempt] = useState(0);
  const [editor, setEditor] = useState<"job" | "resume" | null>(null);
  const [success, setSuccess] = useState("");
  useEffect(() => {
    const controller = new AbortController();
    request<{
      items: { resumeId: string; name: string }[];
      totalCount: number;
    }>(
      `documents?type=Resume&page=${page}`,
      undefined,
      undefined,
      controller.signal,
    )
      .then((p) => {
        if (!controller.signal.aborted) {
          setFiles(p.items);
          setTotal(p.totalCount);
          setFileError("");
        }
      })
      .catch((e) => {
        if (!controller.signal.aborted) setFileError(failure(e));
      });
    return () => controller.abort();
  }, [page, attempt]);
  useEffect(() => {
    const controller = new AbortController();
    request<Matching>(
      `jobs/${jobId}/matching${resumeId ? `?resumeId=${encodeURIComponent(resumeId)}` : ""}`,
      undefined,
      undefined,
      controller.signal,
    )
      .then((value) => {
        if (!controller.signal.aborted) {
          setData(value);
          setError("");
        }
      })
      .catch((e) => {
        if (!controller.signal.aborted) {
          setData(null);
          setError(failure(e));
        }
      });
    return () => controller.abort();
  }, [jobId, resumeId, attempt]);
  const reload = () => {
    setEditor(null);
    setData(null);
    setError("");
    setAttempt((a) => a + 1);
  };
  const result = data?.result;
  return (
    <section className="matching-panel" aria-labelledby="matching-title">
      <h2 id="matching-title">Skill match</h2>
      <p>
        Compare this job with your career profile or one specific resume.
        Required skills earn 3 points, preferred skills 1 point. Score = earned
        ÷ possible points × 100.
      </p>
      <p className="field-hint">
        Exact canonical skill names only; case is ignored, aliases are not
        inferred. This measures recorded skill coverage, not hiring probability,
        proficiency, experience, salary or eligibility. Missing means no
        recorded evidence, not that you lack the skill.
      </p>
      <label htmlFor="matching-source">Compare against</label>
      <select
        id="matching-source"
        value={resumeId}
        disabled={editor !== null}
        onChange={(e) => {
          setResumeId(e.target.value);
          setData(null);
          setSuccess("");
        }}
      >
        <option value="">Career profile skills</option>
        {resumeId && !files.some((f) => f.resumeId === resumeId) && (
          <option value={resumeId}>
            {data?.resume?.name ?? "Selected resume"}
          </option>
        )}
        {files.map((f) => (
          <option key={f.resumeId} value={f.resumeId}>
            {f.name}
          </option>
        ))}
      </select>
      {total > 20 && (
        <div className="detail-actions">
          <button
            className="secondary-button"
            disabled={page === 1 || editor !== null}
            onClick={() => setPage((p) => p - 1)}
          >
            Previous resumes
          </button>
          <span>
            Resume page {page} of {Math.ceil(total / 20)}
          </span>
          <button
            className="secondary-button"
            disabled={page * 20 >= total || editor !== null}
            onClick={() => setPage((p) => p + 1)}
          >
            Next resumes
          </button>
        </div>
      )}
      {fileError && <Feedback error={`Resume choices: ${fileError}`} />}
      <Feedback error={error} success={success} />
      {!data && !error && <p role="status">Loading skill match…</p>}
      {data && result && (
        <>
          <p className="matching-score">
            {result.score === null
              ? "Not scored — add job requirements first"
              : `${result.score}% recorded skill coverage`}
          </p>
          <p>
            <strong>Source:</strong>{" "}
            {data.resume
              ? `${data.resume.name} — user-confirmed resume skills`
              : "Your saved career profile skills"}
            . Results are calculated from current saved data; reload after edits
            elsewhere.
          </p>
          <div className="matching-breakdown">
            <p>
              <strong>Required</strong>
              <br />
              {result.required.matched}/{result.required.total} matched ·{" "}
              {result.required.earnedPoints}/{result.required.possiblePoints}{" "}
              points
            </p>
            <p>
              <strong>Preferred</strong>
              <br />
              {result.preferred.matched}/{result.preferred.total} matched ·{" "}
              {result.preferred.earnedPoints}/{result.preferred.possiblePoints}{" "}
              points
            </p>
          </div>
          {result.skills.length > 0 && (
            <div className="matching-breakdown">
              {[true, false].map((matched) => (
                <div key={String(matched)}>
                  <h3>{matched ? "Matched skills" : "Missing evidence"}</h3>
                  <ul className="matching-skills">
                    {result.skills
                      .filter((s) => s.matched === matched)
                      .map((s) => (
                        <li key={s.skillId}>
                          <strong>{s.name}</strong> · {s.requirementLevel} ·{" "}
                          {s.earnedPoints}/{s.weight} points
                          <br />
                          <span className="field-hint">
                            {s.evidenceRecordedAtUtc
                              ? `${data.source === "Resume" ? "Resume confirmation recorded" : "Profile self-assessment updated"} ${date(s.evidenceRecordedAtUtc)}`
                              : `No ${data.source.toLowerCase()} skill evidence recorded`}
                          </span>
                        </li>
                      ))}
                  </ul>
                  {!result.skills.some((s) => s.matched === matched) && (
                    <p>None.</p>
                  )}
                </div>
              ))}
            </div>
          )}
          {data.resume && data.resume.skills.length === 0 && (
            <p>
              No skills confirmed for this resume yet. Uploading a PDF alone
              does not establish any skills.
            </p>
          )}
          {editor ? (
            <SkillEditor
              key={`${editor}-${data.resume?.id ?? "profile"}`}
              data={data}
              kind={editor}
              close={reload}
              saved={() => {
                setSuccess("Skills saved. Match recalculated.");
                reload();
              }}
            />
          ) : (
            <div className="detail-actions">
              <button
                className="secondary-button"
                onClick={() => setEditor("job")}
              >
                Edit job requirements
              </button>
              {data.resume ? (
                <button
                  className="secondary-button"
                  onClick={() => setEditor("resume")}
                >
                  Confirm resume skills
                </button>
              ) : (
                <Link className="secondary-button" href="/profile">
                  Edit career profile skills
                </Link>
              )}
            </div>
          )}
        </>
      )}
      {!editor && (
        <button className="text-button" onClick={reload}>
          Reload skill match
        </button>
      )}
      <p className="field-hint">
        No AI, external calls or PDF extraction. Your confirmations are evidence
        records, not independent verification. Algorithm: skills-v1.
      </p>
    </section>
  );
}
