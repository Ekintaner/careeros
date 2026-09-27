"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Feedback, Field, useUnsavedChanges } from "@/features/profile/form-ui";
import {
  request,
  safeUrl,
  failure,
  empty,
  workModes,
  employmentTypes,
  seniorityLevels,
  label,
  type Job,
} from "./client";
import { TextField } from "./company-editor";
import { CompanyPicker } from "./shared";

export function JobEditorPage({ id }: { id?: string }) {
  const [initial, setInitial] = useState<Job>();
  const [error, setError] = useState("");
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    if (!id) return;
    const abort = new AbortController();
    request<Job>(`jobs/${id}`, undefined, undefined, abort.signal)
      .then((j) => {
        if (!abort.signal.aborted) setInitial(j);
      })
      .catch((e) => {
        if (!abort.signal.aborted) setError(failure(e));
      });
    return () => abort.abort();
  }, [id, attempt]);
  return (
    <div className="editor-page">
      <Link className="back-link" href={id ? `/jobs/${id}` : "/jobs"}>
        ← {id ? "Job details" : "Jobs"}
      </Link>
      <header className="page-heading">
        <div>
          <p className="eyebrow">Your next move</p>
          <h1>{id ? "Edit job" : "Add job"}</h1>
          <p>Save an opportunity. Keep the context that matters.</p>
        </div>
      </header>
      {!id || initial ? (
        <JobEditor initial={initial} />
      ) : error ? (
        <>
          <Feedback error={error} />
          <button
            className="secondary-button"
            onClick={() => {
              setError("");
              setAttempt(attempt + 1);
            }}
          >
            Try again
          </button>
        </>
      ) : (
        <p role="status">Loading job…</p>
      )}
    </div>
  );
}
function draft(j?: Job) {
  return {
    companyId: j?.companyId ?? "",
    title: j?.title ?? "",
    description: j?.description ?? "",
    location: j?.location ?? "",
    workMode: j?.workMode ?? "",
    employmentType: j?.employmentType ?? "",
    seniorityLevel: j?.seniorityLevel ?? "",
    source: j?.source ?? "",
    sourceUrl: j?.sourceUrl ?? "",
    salaryMin: j?.salaryMin?.toString() ?? "",
    salaryMax: j?.salaryMax?.toString() ?? "",
    salaryCurrency: j?.salaryCurrency ?? "",
    salaryPeriod: j?.salaryPeriod ?? "",
    postedAtUtc: j?.postedAtUtc?.slice(0, 16) ?? "",
    expiresAtUtc: j?.expiresAtUtc?.slice(0, 16) ?? "",
  };
}
export function postingTimestamp(value: string, original?: string | null) {
  // Preserve sub-minute precision when an edit changes unrelated fields.
  if (original && value === original.slice(0, 16)) return original;
  return value ? new Date(value + "Z").toISOString() : null;
}
export function validateJob(v: ReturnType<typeof draft>) {
  const errors: Record<string, string> = {};
  if (!v.title.trim()) errors.title = "Enter a job title.";
  if (!v.companyId) errors.companyId = "Choose one of your companies.";
  if (v.sourceUrl.trim() && !safeUrl(v.sourceUrl.trim()))
    errors.sourceUrl =
      "Use an HTTP or HTTPS URL without credentials or spaces.";
  for (const [key, max] of [
    ["title", 200],
    ["description", 50000],
    ["location", 200],
    ["source", 120],
    ["sourceUrl", 2048],
  ] as const)
    if (v[key].trim().length > max)
      errors[key] = `Use up to ${max} characters.`;
  for (const key of ["salaryMin", "salaryMax"] as const)
    if (
      v[key] &&
      (!/^\d{1,12}(\.\d{1,2})?$/.test(v[key]) ||
        Number(v[key]) > 999999999999.99)
    )
      errors[key] = "Use a nonnegative amount with up to two decimal places.";
  if (v.salaryMin && v.salaryMax && Number(v.salaryMin) > Number(v.salaryMax))
    errors.salaryMax = "Maximum cannot be below minimum.";
  if (v.salaryMin || v.salaryMax) {
    if (!/^[A-Za-z]{3}$/.test(v.salaryCurrency.trim()))
      errors.salaryCurrency = "Use a three-letter currency code.";
    if (!v.salaryPeriod) errors.salaryPeriod = "Choose a pay period.";
  } else if (v.salaryCurrency || v.salaryPeriod)
    errors.salaryMin = "Add a salary amount, or clear currency and pay period.";
  for (const key of ["postedAtUtc", "expiresAtUtc"] as const)
    if (v[key] && Number.isNaN(Date.parse(v[key] + "Z")))
      errors[key] = "Enter a valid UTC date and time.";
  if (v.postedAtUtc && v.expiresAtUtc && v.expiresAtUtc < v.postedAtUtc)
    errors.expiresAtUtc = "Expiration cannot precede posting.";
  return errors;
}
export function JobEditor({ initial }: { initial?: Job }) {
  const router = useRouter();
  const [values, setValues] = useState(() => draft(initial));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const dirty = JSON.stringify(values) !== JSON.stringify(draft(initial));
  useUnsavedChanges(dirty && !busy);
  const change = (key: keyof typeof values, value: string) =>
    setValues((v) => ({ ...v, [key]: value }));
  function text(
    key: keyof typeof values,
    title: string,
    max: number,
    type = "text",
  ) {
    return (
      <Field id={key} title={title} error={errors[key]}>
        <TextField
          id={key}
          value={values[key]}
          maxLength={max}
          disabled={busy}
          invalid={!!errors[key]}
          onChange={(v) => change(key, v)}
          multiline={key === "description"}
          type={type}
        />
      </Field>
    );
  }
  function select(
    key: keyof typeof values,
    title: string,
    options: readonly string[],
  ) {
    return (
      <Field id={key} title={title} error={errors[key]}>
        <select
          id={key}
          value={values[key]}
          disabled={busy}
          aria-invalid={!!errors[key]}
          aria-describedby={errors[key] ? `${key}-hint` : undefined}
          onChange={(e) => change(key, e.target.value)}
        >
          <option value="">Not specified</option>
          {options.map((v) => (
            <option key={v} value={v}>
              {label(v)}
            </option>
          ))}
        </select>
      </Field>
    );
  }
  async function save(e: React.FormEvent) {
    e.preventDefault();
    if (busy) return;
    const found = validateJob(values);
    setErrors(found);
    setError("");
    if (Object.keys(found).length) {
      document.getElementById(Object.keys(found)[0]!)?.focus();
      return;
    }
    setBusy(true);
    try {
      const body = {
        ...values,
        workMode: empty(values.workMode),
        employmentType: empty(values.employmentType),
        seniorityLevel: empty(values.seniorityLevel),
        salaryMin: values.salaryMin === "" ? null : Number(values.salaryMin),
        salaryMax: values.salaryMax === "" ? null : Number(values.salaryMax),
        salaryCurrency: empty(values.salaryCurrency),
        salaryPeriod: empty(values.salaryPeriod),
        postedAtUtc: postingTimestamp(values.postedAtUtc, initial?.postedAtUtc),
        expiresAtUtc: postingTimestamp(
          values.expiresAtUtc,
          initial?.expiresAtUtc,
        ),
        version: initial?.version ?? 0,
      };
      const saved = await request<Job>(
        initial ? `jobs/${initial.id}` : "jobs",
        initial ? "PUT" : "POST",
        body,
      );
      router.push(`/jobs/${saved.id}?saved=1`);
    } catch (e) {
      setError(failure(e));
      setBusy(false);
    }
  }
  return (
    <form noValidate onSubmit={save}>
      <section className="editor-section">
        <div className="section-intro">
          <span className="section-number">01 / The opportunity</span>
          <h2>Start with the essentials</h2>
          <p>A title and one of your companies. Everything else is optional.</p>
        </div>
        <div className="section-fields">
          {text("title", "Job title", 200)}
          <CompanyPicker
            value={values.companyId}
            selectedName={initial?.companyName}
            disabled={busy}
            onChange={(v) => change("companyId", v)}
          />
          {errors.companyId && (
            <p role="alert" className="field-error">
              {errors.companyId}
            </p>
          )}
          {text("location", "Location", 200)}
          <div className="field-pair">
            {select("workMode", "Work mode", workModes)}
            {select("employmentType", "Employment type", employmentTypes)}
          </div>
          {select("seniorityLevel", "Seniority", seniorityLevels)}
        </div>
      </section>
      <section className="editor-section">
        <div className="section-intro">
          <span className="section-number">02 / Context</span>
          <h2>Keep the details</h2>
          <p>
            Paste the description and save the original posting for reference.
            Links are stored without fetching their content.
          </p>
        </div>
        <div className="section-fields">
          {text("source", "Source", 120)}
          {text("sourceUrl", "Original posting URL", 2048)}
          {text("description", "Description", 50000)}
        </div>
      </section>
      <section className="editor-section">
        <div className="section-intro">
          <span className="section-number">03 / Optional details</span>
          <h2>Compensation & timing</h2>
          <p>
            Record the advertised range as written. No currency conversion.
            Posting times are in UTC.
          </p>
        </div>
        <div className="section-fields">
          <div className="field-pair">
            {text("salaryMin", "Minimum salary", 15)}
            {text("salaryMax", "Maximum salary", 15)}
          </div>
          <div className="field-pair">
            {text("salaryCurrency", "Currency", 3)}
            {select("salaryPeriod", "Pay period", ["Hour", "Month", "Year"])}
          </div>
          <div className="field-pair">
            {text("postedAtUtc", "Posted at (UTC)", 30, "datetime-local")}
            {text("expiresAtUtc", "Expires at (UTC)", 30, "datetime-local")}
          </div>
        </div>
      </section>
      <Feedback error={error} />
      <div className="editor-actions">
        <span className="field-hint">
          {dirty ? "You have unsaved changes." : ""}
        </span>
        <button
          type="button"
          className="secondary-button"
          disabled={busy}
          onClick={() => {
            if (!dirty || window.confirm("Discard your changes?"))
              router.push(initial ? `/jobs/${initial.id}` : "/jobs");
          }}
        >
          Cancel
        </button>
        <button className="primary-button" disabled={busy}>
          {busy ? "Saving…" : "Save job"}
        </button>
      </div>
      {error && initial && (
        <button
          type="button"
          className="text-button"
          onClick={() => {
            if (window.confirm("Discard your draft and reload the latest job?"))
              window.location.reload();
          }}
        >
          Reload latest job
        </button>
      )}
    </form>
  );
}
