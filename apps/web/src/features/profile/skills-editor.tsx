"use client";
import { useEffect, useRef, useState } from "react";
import {
  experienceError,
  failure,
  proficiencies,
  read,
  write,
  type Suggestion,
  type UserSkill,
} from "./profile-client";
import { Feedback, Field } from "./form-ui";

export function SkillsEditor() {
  const [skills, setSkills] = useState<UserSkill[]>();
  const [name, setName] = useState("");
  const [suggestions, setSuggestions] = useState<Suggestion[]>([]);
  const [searchError, setSearchError] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [busy, setBusy] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const nameRef = useRef<HTMLInputElement>(null);
  useEffect(() => {
    const abort = new AbortController();
    read<UserSkill[]>("profile/skills", abort.signal)
      .then(setSkills)
      .catch(() => {
        if (!abort.signal.aborted) setError("We couldn’t load your skills.");
      });
    return () => abort.abort();
  }, [attempt]);
  useEffect(() => {
    const abort = new AbortController();
    if (name.trim().length < 2) return;
    const timer = setTimeout(() => {
      read<Suggestion[]>(
        `skills?query=${encodeURIComponent(name.trim())}`,
        abort.signal,
      )
        .then((result) => {
          if (!abort.signal.aborted) {
            setSuggestions(result);
            setSearchError(false);
          }
        })
        .catch(() => {
          if (!abort.signal.aborted) {
            setSuggestions([]);
            setSearchError(true);
          }
        });
    }, 250);
    return () => {
      clearTimeout(timer);
      abort.abort();
    };
  }, [name]);
  async function add(event: React.FormEvent) {
    event.preventDefault();
    if (busy) return;
    setError("");
    setSuccess("");
    const clean = name.trim();
    if (!clean || clean.length > 100 || /[^\x20-\x7e]/.test(clean)) {
      setError("Enter a skill using 1–100 printable ASCII characters.");
      return;
    }
    setBusy(true);
    try {
      const result = await write<UserSkill>("profile/skills", "POST", {
        name: clean,
        proficiency: null,
        yearsExperience: null,
      });
      if (!result) throw new Error("Missing skill");
      setSkills((items) =>
        [...(items ?? []), result].sort((a, b) => a.name.localeCompare(b.name)),
      );
      setName("");
      setSuggestions([]);
      setSuccess(`${result.name} added.`);
    } catch (e) {
      setError(failure(e));
    } finally {
      setBusy(false);
    }
  }
  return (
    <section
      className="editor-section skills-section"
      aria-labelledby="skills-title"
    >
      <div className="section-intro">
        <span className="section-number">03 / Your strengths</span>
        <h2 id="skills-title">Skills</h2>
        <p>
          Build a clear picture of your capabilities. Experience and proficiency
          are optional.
        </p>
        <p className="field-hint">
          Skill names are shared vocabulary. Keep personal details in your
          profile.
        </p>
      </div>
      <div className="section-fields">
        {skills ? (
          <>
            <form className="skill-add" onSubmit={add} noValidate>
              <Field
                id="skill-name"
                title="Find or add a skill"
                hint="Search existing names, or add your own. Up to 100 characters."
              >
                <input
                  ref={nameRef}
                  id="skill-name"
                  value={name}
                  maxLength={100}
                  autoComplete="off"
                  disabled={busy}
                  aria-describedby="skill-name-hint"
                  onChange={(e) => {
                    setName(e.target.value);
                    setSuggestions([]);
                    setSearchError(false);
                    setSuccess("");
                  }}
                />
              </Field>
              <button
                className="primary-button"
                disabled={busy || !name.trim()}
              >
                {busy ? "Adding…" : "Add skill"}
              </button>
            </form>
            {name.trim().length >= 2 && suggestions.length > 0 && (
              <ul className="skill-suggestions" aria-label="Matching skills">
                {suggestions.map((s) => (
                  <li key={s.id}>
                    <button
                      type="button"
                      disabled={busy}
                      onClick={() => {
                        setName(s.name);
                        setSuggestions([]);
                        nameRef.current?.focus();
                      }}
                    >
                      {s.name}
                    </button>
                  </li>
                ))}
              </ul>
            )}
            {searchError && (
              <p role="status" className="field-hint">
                Suggestions are unavailable. You can still add a skill by name.
              </p>
            )}
            {skills.length === 0 ? (
              <p className="editor-empty">No skills added yet.</p>
            ) : (
              <ul className="skill-list">
                {skills.map((skill) => (
                  <li key={skill.skillId}>
                    <SkillRow
                      skill={skill}
                      onSaved={(saved) =>
                        setSkills((items) =>
                          items?.map((s) =>
                            s.skillId === saved.skillId ? saved : s,
                          ),
                        )
                      }
                      onRemoved={() => {
                        setSkills((items) =>
                          items?.filter((s) => s.skillId !== skill.skillId),
                        );
                        setSuccess(`${skill.name} removed.`);
                        nameRef.current?.focus();
                      }}
                    />
                  </li>
                ))}
              </ul>
            )}
          </>
        ) : (
          !error && <p role="status">Loading skills…</p>
        )}
        <Feedback error={error} success={success} />
        {error && (
          <button
            type="button"
            className="text-button"
            disabled={busy}
            onClick={() => {
              setError("");
              setAttempt(attempt + 1);
            }}
          >
            Reload skills
          </button>
        )}
      </div>
    </section>
  );
}
function SkillRow({
  skill,
  onSaved,
  onRemoved,
}: {
  skill: UserSkill;
  onSaved: (skill: UserSkill) => void;
  onRemoved: () => void;
}) {
  const [proficiency, setProficiency] = useState(skill.proficiency ?? "");
  const [years, setYears] = useState(skill.yearsExperience?.toString() ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [saved, setSaved] = useState(false);
  const dirty =
    proficiency !== (skill.proficiency ?? "") ||
    years !== (skill.yearsExperience?.toString() ?? "");
  async function save(event: React.FormEvent) {
    event.preventDefault();
    if (busy) return;
    const invalid = experienceError(years);
    setError(invalid);
    if (invalid) return;
    setBusy(true);
    try {
      const result = await write<UserSkill>(
        `profile/skills/${skill.skillId}`,
        "PUT",
        {
          proficiency: proficiency || null,
          yearsExperience: years === "" ? null : Number(years),
          version: skill.version,
        },
      );
      if (!result) throw new Error("Missing skill");
      onSaved(result);
      setSaved(true);
    } catch (e) {
      setError(failure(e));
    } finally {
      setBusy(false);
    }
  }
  return (
    <form onSubmit={save} noValidate aria-label={`${skill.name} details`}>
      <strong className="skill-title">{skill.name}</strong>
      <div className="skill-controls">
        <Field
          id={`proficiency-${skill.skillId}`}
          title={`Proficiency for ${skill.name}`}
        >
          <select
            id={`proficiency-${skill.skillId}`}
            value={proficiency}
            disabled={busy}
            onChange={(e) => {
              setProficiency(e.target.value);
              setSaved(false);
            }}
          >
            <option value="">Not specified</option>
            {proficiencies.map((p) => (
              <option key={p}>{p}</option>
            ))}
          </select>
        </Field>
        <Field id={`years-${skill.skillId}`} title={`Years with ${skill.name}`}>
          <input
            id={`years-${skill.skillId}`}
            inputMode="decimal"
            maxLength={5}
            value={years}
            disabled={busy}
            onChange={(e) => {
              setYears(e.target.value);
              setSaved(false);
            }}
          />
        </Field>
        <button className="secondary-button" disabled={busy || !dirty}>
          {busy ? "Saving…" : "Save skill"}
        </button>
        <button
          className="text-button"
          type="button"
          disabled={busy}
          aria-label={`Remove ${skill.name}`}
          onClick={async () => {
            if (busy) return;
            setBusy(true);
            setError("");
            try {
              await write(
                `profile/skills/${skill.skillId}?version=${skill.version}`,
                "DELETE",
              );
              onRemoved();
            } catch (e) {
              setError(failure(e));
            } finally {
              setBusy(false);
            }
          }}
        >
          Remove
        </button>
      </div>
      <Feedback error={error} success={saved ? "Skill updated." : undefined} />
      {error && (
        <p className="field-hint">
          If this skill changed elsewhere, reload the page before editing again.
        </p>
      )}
    </form>
  );
}
