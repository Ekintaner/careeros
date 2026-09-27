"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Feedback, Field, useUnsavedChanges } from "@/features/profile/form-ui";
import { request, safeUrl, failure, type Company } from "./client";

export function CompanyEditorPage({ id }: { id?: string }) {
  const [initial, setInitial] = useState<Company>();
  const [error, setError] = useState("");
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    if (!id) return;
    const abort = new AbortController();
    request<Company>(`companies/${id}`, undefined, undefined, abort.signal)
      .then((c) => {
        if (!abort.signal.aborted) setInitial(c);
      })
      .catch((e) => {
        if (!abort.signal.aborted) setError(failure(e));
      });
    return () => abort.abort();
  }, [id, attempt]);
  return (
    <div className="editor-page">
      <Link className="back-link" href="/companies">
        ← Companies
      </Link>
      <header className="page-heading">
        <div>
          <p className="eyebrow">Your network</p>
          <h1>{id ? "Edit company" : "Add company"}</h1>
          <p>A useful home for the opportunities you save.</p>
        </div>
      </header>
      {!id || initial ? (
        <CompanyEditor key={initial?.version ?? "new"} initial={initial} />
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
        <p role="status">Loading company…</p>
      )}
    </div>
  );
}
export function CompanyEditor({ initial }: { initial?: Company }) {
  const router = useRouter();
  const start = {
    name: initial?.name ?? "",
    website: initial?.website ?? "",
    industry: initial?.industry ?? "",
    location: initial?.location ?? "",
    description: initial?.description ?? "",
  };
  const [values, setValues] = useState(start);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const dirty = JSON.stringify(values) !== JSON.stringify(start);
  useUnsavedChanges(dirty && !busy);
  async function save(e: React.FormEvent) {
    e.preventDefault();
    if (busy) return;
    const found: Record<string, string> = {};
    if (!values.name.trim()) found.name = "Enter a company name.";
    if (values.website.trim() && !safeUrl(values.website.trim()))
      found.website = "Use an HTTP or HTTPS URL without credentials or spaces.";
    for (const [key, max] of [
      ["name", 200],
      ["website", 2048],
      ["industry", 120],
      ["location", 200],
      ["description", 10000],
    ] as const)
      if (values[key].trim().length > max)
        found[key] = `Use up to ${max} characters.`;
    setErrors(found);
    setError("");
    if (Object.keys(found).length) {
      document.getElementById(Object.keys(found)[0]!)?.focus();
      return;
    }
    setBusy(true);
    try {
      await request<Company>(
        initial ? `companies/${initial.id}` : "companies",
        initial ? "PUT" : "POST",
        { ...values, version: initial?.version ?? 0 },
      );
      router.push("/companies?saved=1");
    } catch (e) {
      setError(failure(e));
      setBusy(false);
    }
  }
  return (
    <form noValidate onSubmit={save}>
      <section className="editor-section">
        <div className="section-intro">
          <span className="section-number">Company details</span>
          <h2>The essentials</h2>
          <p>
            Only a name is required. Add context when it helps you recognize the
            company.
          </p>
        </div>
        <div className="section-fields">
          {(
            [
              ["name", "Company name", 200],
              ["website", "Website", 2048],
              ["industry", "Industry", 120],
              ["location", "Location", 200],
              ["description", "Description", 10000],
            ] as const
          ).map(([key, title, max]) => (
            <Field key={key} id={key} title={title} error={errors[key]}>
              <TextField
                id={key}
                multiline={key === "description"}
                value={values[key]}
                maxLength={max}
                disabled={busy}
                invalid={!!errors[key]}
                onChange={(value) => setValues((v) => ({ ...v, [key]: value }))}
              />
            </Field>
          ))}
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
              router.push("/companies");
          }}
        >
          Cancel
        </button>
        <button className="primary-button" disabled={busy}>
          {busy ? "Saving…" : "Save company"}
        </button>
      </div>
      {error && initial && (
        <button
          type="button"
          className="text-button"
          onClick={() => {
            if (
              window.confirm(
                "Discard your draft and reload the latest company?",
              )
            )
              window.location.reload();
          }}
        >
          Reload latest company
        </button>
      )}
    </form>
  );
}
export function TextField({
  id,
  multiline,
  value,
  maxLength,
  disabled,
  invalid,
  onChange,
  type = "text",
}: {
  id: string;
  multiline?: boolean;
  value: string;
  maxLength?: number;
  disabled?: boolean;
  invalid?: boolean;
  onChange: (value: string) => void;
  type?: string;
}) {
  const props = {
    id,
    value,
    maxLength,
    disabled,
    "aria-invalid": invalid,
    "aria-describedby": invalid ? `${id}-hint` : undefined,
    onChange: (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) =>
      onChange(e.target.value),
  };
  return multiline ? (
    <textarea {...props} rows={6} />
  ) : (
    <input {...props} type={type} />
  );
}
