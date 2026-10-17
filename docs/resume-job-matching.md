# Resume and job skill matching (CODEX-008)

## Using the feature

Open any job detail and find **Skill match**. Start with **Edit job requirements**: enter one canonical skill name per line under Required or Preferred. These are manually checked requirements from the posting, not inferred from text. Saving requirements never adds claims to your career profile or resume.

Choose **Career profile skills** to compare against the skills you explicitly saved in `/profile`. Choose a PDF resume to compare only against that resume's confirmed skills. Resume choices use the existing 20-item document pagination, including older resumes. Use **Confirm resume skills**, check the exact PDF yourself, enter the supported names and confirm the evidence checkbox before saving. Uploading a PDF supplies no skill evidence. No profile skills are copied into resumes. All names entered in these editors become shared canonical vocabulary, so keep personal information out of names.

Every requirement appears under Matched skills or Missing evidence, with its importance, earned/possible points and the source record's timestamp. Extra candidate skills do not affect the score. Resume confirmations retained during edits keep their original recorded time; remove/re-add to record a new assertion. Profile timestamps describe saved self-assessments. Neither source constitutes independent verification of ability or PDF contents.

## Deterministic scoring: skills-v1

- Identity: existing canonical skill UUID, resolved from trimmed printable ASCII names with the existing ASCII case normalization. No synonyms, substring matching, stemming, Unicode folding or external dictionary. `SQL` and `sql` resolve to the same skill; `C`, `C++`, `C#`, `.NET`, `ASP.NET` remain distinct. Significant internal spaces and punctuation remain significant.
- Required weight = 3; Preferred weight = 1.
- A requirement earns all its weight if its UUID exists in the selected source, otherwise zero. No partial credit, keyword frequency bonus or extra-skill bonus.
- Score = `100 * earnedPoints / possiblePoints`, rounded to one decimal, midpoint away from zero. For example, one matched required skill and one missing preferred skill yields `3 / 4 * 100 = 75%`.
- Required and Preferred each expose matched count, total count, earned points and possible points. Every row exposes the exact contribution and optional evidence timestamp. Rows are sorted Required first, then name using ordinal case-insensitive ordering, then UUID.
- No requirements: score is `null` / Not scored, never 0 or 100. Nonempty requirements with an empty source: 0. Preferred-only requirements use their actual denominator. The UI calls this **recorded skill coverage**, not hiring probability.
- Profile and resume are mutually exclusive sources. A profile match can never establish evidence in a resume. Proficiency, years of experience, location, work authorization, seniority, salary and preferences are not scored. Matching names does not prove proficiency, recency, eligibility or job suitability.

## API and ownership

All routes require the existing validated session. Writes retain trusted Origin and CSRF-header checks; responses retain no-store headers. No owner is accepted in a request body or returned in DTOs. Unknown JSON properties are rejected, including inside a requirement. Foreign and missing jobs/resumes return the same 404.

| Method | Route                                      | Contract                                                                                              |
| ------ | ------------------------------------------ | ----------------------------------------------------------------------------------------------------- |
| GET    | `/api/v1/jobs/{id}/matching`               | Current profile match, job xmin version, algorithm and full explanation                               |
| GET    | `/api/v1/jobs/{id}/matching?resumeId={id}` | Match against this owned resume only; includes its name, xmin version and confirmed skills            |
| GET    | `/api/v1/resumes/{id}/skills`              | Owned resume skill evidence and version for editing                                                   |
| PUT    | `/api/v1/jobs/{id}/skills`                 | `{ version, skills: [{ name, requirementLevel: "Required" or "Preferred" }] }`; full replacement; 204 |
| PUT    | `/api/v1/resumes/{id}/skills`              | `{ version, names: ["C#", "SQL"] }`; explicit user assertions; full replacement; 204                  |

At most 100 requirements/confirmed resume skills, at most 100 characters per canonical name. Empty arrays intentionally clear the set. Empty names, duplicate normalized names (including across Required/Preferred), invalid enum strings, null arrays/items and zero versions are rejected. Existing product request size ceilings also apply. Oversized bodies are rejected by Kestrel, not deserialized unboundedly. The evidence checkbox is a UI acknowledgment, not a security or authenticity claim; authenticated API clients can explicitly submit their own assertions.

All private reads/writes use OwnedData. Canonical names are intentionally shared vocabulary; parameterized insert-or-reuse mirrors the existing profile policy. No shared vocabulary deletion or rename occurs. Creating a canonical requirement does not add a UserSkill or ResumeSkill.

## Consistency and performance

Each full-set replacement runs in one PostgreSQL transaction, checks the parent job/resume xmin and forces an audit update before changing associations. This acquires a parent row lock and advances xmin even for a same-value replacement. Resume writes acquire the same owner advisory lock as document deletion/primary selection before touching the parent or skill rows, preventing lifecycle lock inversion. Concurrent/stale replacements return 409 with no partial association changes or vocabulary inserts. Shared inserts are sorted by normalized name to reduce lock-order conflicts. Foreign-key races are safe conflicts; other unexpected storage failures remain generic 500s and roll back. Existing job/profile/application concurrency and document restriction behavior remain authoritative.

Matching is calculated on demand inside a REPEATABLE READ transaction containing only reads, so requirements, selected source and explanation come from one database snapshot. Nothing is persisted or cached as a score. Profile mode executes three data queries, resume mode four, independent of skill count; authentication queries are separate. Association/name/evidence queries project only needed columns; profile mode filters evidence by requirement IDs in SQL. Existing composite owner/job/skill and owner/resume/skill keys support lookups. Parent lookups project only the version/identity/name needed and use existing key indexes. No PDF bytes, filesystem paths, job-description scans or unbounded shared vocabulary scans are needed. Writes perform at most 100 insert-or-reuse name lookups, acceptable for manual bounded saves; no per-skill read round trips occur during matching.

A result is a snapshot, not a live subscription. Reload after changes elsewhere. Skill edits advance the job's normal version, so an already-open job archive/edit action may require its existing **Reload latest job** flow. Unsaved editor drafts are retained on error/conflict; conflicts require explicit discard/reload. Browser unload warns on changed text; client-side navigation does not preserve drafts. Whole-set editors deliberately avoid background autosave.

## Storage, cost and limits

Reuses JobSkill, ResumeSkill, UserSkill and Skill tables. No migration, package, configuration secret, parser, AI, paid API, mandatory cloud or external service added. Matching requires the existing local/self-hostable application and PostgreSQL. Existing Redis readiness and private PDF storage are unchanged.

This phase provides manual, explainable skill matching. It does not extract PDF text, OCR documents, verify claims, infer skills from job prose, resolve aliases, rank every job in a batch, persist historical scores or predict hiring. PDF scanning/backup limits from CODEX-007 still apply. Required gaps remain explicitly visible even when preferred matches raise the weighted score; no automatic rejection or candidate selection is performed.
