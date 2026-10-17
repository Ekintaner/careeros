# Skills intelligence and gap analytics (CODEX-009)

## Workflow and source of truth

Open **Skills intelligence** in the Career navigation (`/skills`). The page analyzes your saved jobs' explicitly recorded JobSkill requirements, using career-profile UserSkill records and user-confirmed ResumeSkill records. It never parses descriptions/PDFs or invents skills from prose. Add or edit requirements in a job's CODEX-008 **Skill match** panel. Update claimed skills in `/profile`; confirm resume evidence on the matching panel for the exact resume that supports it.

Active jobs are the default scope. Archived and All saved jobs are explicit options. Each job counts once per canonical skill because the existing association key is unique. Jobs without structured requirements are counted as unassessed, rather than treated as having no requirements. The assessed-job denominator excludes them. All values describe this personal collection, not the external job market or all opportunities available to the user.

Summary metrics use the whole selected scope. Search, evidence status, sort and skill-page controls affect only the skill list; they do not silently change summary denominators or priority values. Names and punctuation retain CODEX-008 canonical identity and case normalization. Extra profile skills not requested by this job scope are intentionally outside this view.

## Distinguishing ability records from evidence gaps

The page shows both source flags and three non-overlapping evidence categories:

| Category                             | Career profile record | Confirmed resume record | Suggested action                                      |
| ------------------------------------ | --------------------- | ----------------------- | ----------------------------------------------------- |
| Profile + resume (`supported`)       | Present               | At least one            | Maintain accurate records                             |
| Evidence to complete (`evidenceGap`) | Present               | None                    | Document genuine resume evidence                      |
| Evidence to complete (`evidenceGap`) | Absent                | At least one            | Review the profile record                             |
| No recorded skill (`missing`)        | Absent                | None                    | Assess existing ability first, then consider learning |

A missing record is not a conclusion that the user lacks a skill. Profile-only and resume-only skills are not labeled learning deficits. Profile/resume membership remains an explicit user assertion, not independent verification; no confidence score, proficiency inference or evidence-age cutoff is invented. A confirmed resume count counts distinct owned ResumeSkill associations (one per resume/skill), not independent endorsements. A resume's evidence does not need to be assigned to a job application to be included; this view checks recorded evidence anywhere in the user's resume collection, not suitability of a specific application CV. Use job matching for a specific CV comparison.

## Explainable demand and priorities

Uses `MatchingRules.RequiredWeight = 3`, `PreferredWeight = 1`, and the same `MatchingRules.Percentage` rounding as CODEX-008. The extracted constants/helper preserve every existing matching result; its tests remain in the combined suite.

- Weighted demand = `3 × required job count + preferred job count`.
- Demand share = `job count requesting skill / assessed job count × 100`.
- Base action priority = `weighted demand / (3 × assessed job count) × 100`.
- A supported skill has action priority 0 because both source records exist.
- Learning/assessment priority equals base action priority only when neither source records the skill. Otherwise learning priority is 0. For single-source skills, the base value is explicitly an **evidence review priority**.
- Values round to one decimal, midpoint away from zero. Empty denominators yield no coverage score; list priorities use 0 only for a genuinely empty demand calculation, not invented observations.
- Priority is a deterministic demand heuristic, not a hiring probability, ability score, learning-time estimate, salary prediction or instruction to learn before checking existing experience.

Example: two assessed jobs both require Docker. Weighted demand = 6, demand share = 100%, base priority = `6/(3×2)×100 = 100`. If neither source records Docker, its learning/assessment priority is 100. If it is only in the profile, learning priority is 0 and evidence review priority is 100. If both sources record it, action priority is 0.

Default sorting uses descending action priority, then no-record skills before one-source records on ties, then demand count, normalized name and UUID. With a fixed scope, ranking by base priority is equivalent to ranking by weighted demand; supported records receive zero. Other allowlisted sorts are Most requested (job count, Required count, name, UUID) and Skill name (normalized name, UUID). Each has deterministic ties.

Aggregate profile coverage = `sum of matched profile requirement points / sum of all requirement points × 100`. This is weighted across requirement rows, **not the average of job scores**. Resume evidence never inflates this profile metric. Each expanded skill shows the jobs requesting it, their requirement level, archived state and complete-job profile matching score, reusing the same weights/rounding. In integration tests those scores are compared directly with CODEX-008 matching responses.

## History policy

There are no immutable requirement/evidence snapshots in the existing schema. Current job creation, posting and requirement edit timestamps cannot reconstruct past demand. `historyAvailable` is therefore false and the UI explicitly says trends are unavailable. No time-series chart, growth rate or historical statistic is fabricated. Reliable future history would require a separately designed, explicit snapshot/event feature; no migration is added speculatively.

## API, validation and security

| Method | Route                                        | Behavior                                                                                       |
| ------ | -------------------------------------------- | ---------------------------------------------------------------------------------------------- |
| GET    | `/api/v1/intelligence/skills`                | Current summary plus a bounded, filtered skill page                                            |
| GET    | `/api/v1/intelligence/skills/{skillId}/jobs` | Paginated owned jobs requesting that skill in the selected scope, with full-job profile scores |

Both endpoints require the existing validated session and return no-store responses. They are read-only. All private job, company, skill-association, profile-association and resume-association queries use OwnedData. Only canonical vocabulary joins read shared Skill rows. A globally known skill not demanded in the current user's selected job scope returns 404 on drilldown, just like an unknown UUID. Responses do not expose owners, private document paths, PDF bytes, credentials or other users' job data.

Parameters: `page` (1–10,000), `pageSize` (1–50; default 20), `state` (`active`, `archived`, `all`), `status` (`all`, `supported`, `evidenceGap`, `missing`), `sort` (`priority`, `demand`, `name`), `search` (up to 100 printable ASCII characters consistent with canonical names; blank means no filter). Invalid bounds/enums/search return 400. Search uses a parameterized literal normalized substring, not executable SQL or wildcard interpolation. Skill drilldown validates the same parameter contract; status/search/sort concern the outer skill list and do not filter the list of jobs requesting an already selected skill. Its jobs sort deterministically by title then UUID. Out-of-range pages return empty rows with current counts.

No owner can be selected by request data. Read-only requests do not add profile/resume claims or shared vocabulary. React renders names and job titles as escaped text. Browser request cancellation and keyed responses discard obsolete results after scope/filter changes; no persistent browser product cache is added. Errors have honest retry states; drilldown errors do not erase the analysis.

## Query shape and consistency

Each analysis response uses one REPEATABLE READ transaction and four database commands: scoped job counts, aggregate demand/evidence/coverage summary, filtered skill count, ordered projected skill page. Grouping, filtering, counting and pagination occur in PostgreSQL. Only at most 50 skill rows are materialized for DTO/formula construction; no complete user's job/skill catalogue is loaded into memory. The evidence summary uses owner-scoped left joins and aggregate resume counts. Parent job scope is expressed as a key-only subquery to avoid unnecessary entity columns. Existing composite owner/skill/job/resume keys and owner/archive access patterns suffice for the current personal workload.

A drilldown uses a separate consistent read transaction and three commands: authorize the demanded skill/name in the owner's scope, count related rows, then project/order/page jobs and their weighted profile points. It returns no descriptions or document payloads. The UI fetches a drilldown only when requested and pages it at 10 rows. There is no per-skill round-trip loop, stored score, background polling or mandatory service. Counts are snapshots, not live subscriptions; use Reload analysis after edits elsewhere. Aggregate work scales with the owner's relevant requirement associations; no performance claims about an unbounded enterprise dataset are made.

## Cost and limits

No package, migration, configuration secret, paid AI/API/SaaS, cloud resource, market dataset or mandatory external dependency added. Runs on the existing local/self-hostable frontend, API and PostgreSQL; Redis readiness and private document storage are unchanged. Skills intelligence is a current, personal, manually curated demand view. It does not assess proficiency, work authorization, learning feasibility, job quality or hiring outcomes and does not perform automatic candidate decisions.
