"use client";
import { useEffect, useState } from "react";
import {
  employmentTypes,
  experienceError,
  failure,
  label,
  lines,
  preferenceError,
  read,
  seniorityLevels,
  workModes,
  write,
  type Profile,
} from "./profile-client";
import { Feedback, Field, useUnsavedChanges } from "./form-ui";
import { SkillsEditor } from "./skills-editor";

export function ProfilePage() {
  const [profile, setProfile] = useState<Profile>();
  const [error, setError] = useState(false);
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    const abort = new AbortController();
    read<Profile>("profile", abort.signal)
      .then(setProfile)
      .catch(() => {
        if (!abort.signal.aborted) setError(true);
      });
    return () => abort.abort();
  }, [attempt]);
  return (
    <div className="editor-page">
      <header className="page-heading">
        <div>
          <p className="eyebrow">Your career</p>
          <h1>Career profile</h1>
          <p>The experience you bring. The work you want next.</p>
        </div>
        <span className="outline-label">Personal workspace</span>
      </header>
      {profile ? (
        <>
          <ProfileEditor initial={profile} onSaved={setProfile} />
          <SkillsEditor />
        </>
      ) : error ? (
        <div role="alert">
          <p>We couldn’t load your profile.</p>
          <button
            className="secondary-button"
            onClick={() => {
              setError(false);
              setAttempt(attempt + 1);
            }}
          >
            Try again
          </button>
        </div>
      ) : (
        <p role="status">Loading your profile…</p>
      )}
    </div>
  );
}
function draft(profile: Profile) {
  return {
    headline: profile.headline ?? "",
    summary: profile.summary ?? "",
    currentLocation: profile.currentLocation ?? "",
    experience: profile.yearsOfExperience?.toString() ?? "",
    preferredWorkMode: profile.preferredWorkMode ?? "",
    roles: profile.targetRoles.join("\n"),
    locations: profile.preferredLocations.join("\n"),
    employmentTypes: profile.employmentTypes,
    seniorityLevels: profile.seniorityLevels,
  };
}
function ProfileEditor({
  initial,
  onSaved,
}: {
  initial: Profile;
  onSaved: (p: Profile) => void;
}) {
  const [values, setValues] = useState(() => draft(initial));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const dirty = JSON.stringify(values) !== JSON.stringify(draft(initial));
  useUnsavedChanges(dirty);
  function change(key: keyof typeof values, value: string | string[]) {
    setValues((v) => ({ ...v, [key]: value }));
    setSuccess("");
  }
  async function save(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    const found = {
      experience: experienceError(values.experience),
      roles: preferenceError(values.roles, 100),
      locations: preferenceError(values.locations, 200),
      headline:
        values.headline.trim().length > 200 ? "Use up to 200 characters." : "",
      summary:
        values.summary.trim().length > 10000
          ? "Use up to 10,000 characters."
          : "",
      currentLocation:
        values.currentLocation.trim().length > 200
          ? "Use up to 200 characters."
          : "",
    };
    setErrors(found);
    setSuccess("");
    setError("");
    if (Object.values(found).some(Boolean)) {
      const key = Object.keys(found).find(
        (k) => found[k as keyof typeof found],
      );
      document.getElementById(key!)?.focus();
      return;
    }
    setBusy(true);
    try {
      const saved = await write<Profile>("profile", "PUT", {
        headline: values.headline,
        summary: values.summary,
        currentLocation: values.currentLocation,
        yearsOfExperience:
          values.experience === "" ? null : Number(values.experience),
        preferredWorkMode: values.preferredWorkMode || null,
        targetRoles: lines(values.roles),
        preferredLocations: lines(values.locations),
        employmentTypes: values.employmentTypes,
        seniorityLevels: values.seniorityLevels,
        version: initial.version,
      });
      if (!saved) throw new Error("Missing profile");
      onSaved(saved);
      setValues(draft(saved));
      setSuccess("Profile saved.");
    } catch (e) {
      setError(failure(e));
    } finally {
      setBusy(false);
    }
  }
  function text(
    key:
      | "headline"
      | "summary"
      | "currentLocation"
      | "experience"
      | "roles"
      | "locations",
    title: string,
    max: number,
    hint?: string,
    multiline = false,
  ) {
    const props = {
      id: key,
      value: values[key],
      maxLength: max,
      disabled: busy,
      "aria-invalid": !!errors[key],
      "aria-describedby": `${key}-hint`,
      onChange: (
        e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>,
      ) => change(key, e.target.value),
    };
    return (
      <Field id={key} title={title} error={errors[key]} hint={hint}>
        {multiline ? (
          <textarea {...props} rows={key === "summary" ? 5 : 3} />
        ) : (
          <input
            {...props}
            inputMode={key === "experience" ? "decimal" : undefined}
          />
        )}
      </Field>
    );
  }
  function choices(
    key: "employmentTypes" | "seniorityLevels",
    title: string,
    options: readonly string[],
  ) {
    return (
      <fieldset className="preference-options">
        <legend>{title}</legend>
        <div>
          {options.map((option) => (
            <label key={option}>
              <input
                type="checkbox"
                checked={values[key].includes(option)}
                disabled={busy}
                onChange={(e) =>
                  change(
                    key,
                    e.target.checked
                      ? [...values[key], option]
                      : values[key].filter((v) => v !== option),
                  )
                }
              />
              {label(option)}
            </label>
          ))}
        </div>
      </fieldset>
    );
  }
  return (
    <form onSubmit={save} noValidate>
      <section className="editor-section" aria-labelledby="career-basics">
        <div className="section-intro">
          <span className="section-number">01 / Your background</span>
          <h2 id="career-basics">Career profile</h2>
          <p>
            A concise introduction to your professional experience. Every field
            is optional.
          </p>
        </div>
        <div className="section-fields">
          {text(
            "headline",
            "Professional headline",
            200,
            "A short description of the work you do.",
          )}
          {text(
            "summary",
            "Professional summary",
            10000,
            "Up to 10,000 characters.",
            true,
          )}
          <div className="field-pair">
            {text(
              "currentLocation",
              "Current location",
              200,
              "City or region; no home address needed.",
            )}
            {text(
              "experience",
              "Years of experience",
              5,
              "Optional · 0–100 years",
            )}
          </div>
        </div>
      </section>
      <section className="editor-section" aria-labelledby="job-preferences">
        <div className="section-intro">
          <span className="section-number">02 / Your next move</span>
          <h2 id="job-preferences">Job preferences</h2>
          <p>
            Add the roles you’re targeting so CareerOS can personalize future
            job analysis.
          </p>
        </div>
        <div className="section-fields">
          <div className="field-pair">
            {text(
              "roles",
              "Target roles",
              1010,
              "One role per line · up to 10",
              true,
            )}
            {text(
              "locations",
              "Preferred locations",
              2010,
              "One city or region per line · up to 10",
              true,
            )}
          </div>
          <Field id="work-mode" title="Preferred work mode">
            <select
              id="work-mode"
              value={values.preferredWorkMode}
              disabled={busy}
              onChange={(e) => change("preferredWorkMode", e.target.value)}
            >
              <option value="">No preference</option>
              {workModes.map((v) => (
                <option key={v}>{v}</option>
              ))}
            </select>
          </Field>
          {choices("employmentTypes", "Employment types", employmentTypes)}
          {choices("seniorityLevels", "Seniority levels", seniorityLevels)}
        </div>
      </section>
      <div className="editor-actions">
        <span className="field-hint">
          {dirty ? "You have unsaved changes." : "Your profile is up to date."}
        </span>
        <button
          type="button"
          className="secondary-button"
          disabled={busy || !dirty}
          onClick={() => {
            setValues(draft(initial));
            setErrors({});
            setError("");
          }}
        >
          Discard changes
        </button>
        <button className="primary-button" disabled={busy || !dirty}>
          {busy ? "Saving…" : "Save profile"}
        </button>
      </div>
      <Feedback error={error} success={success} />
      {error && (
        <button
          type="button"
          className="text-button"
          disabled={busy}
          onClick={async () => {
            if (
              dirty &&
              !window.confirm(
                "Discard your unsaved changes and load the latest profile?",
              )
            )
              return;
            setBusy(true);
            try {
              const p = await read<Profile>("profile");
              onSaved(p);
              setValues(draft(p));
              setError("");
            } catch {
              setError("Couldn’t reload your profile. Try again.");
            } finally {
              setBusy(false);
            }
          }}
        >
          Reload latest profile
        </button>
      )}
    </form>
  );
}
