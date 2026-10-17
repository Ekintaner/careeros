# CODEX-009 and combined CODEX-008/009 verification

## Preservation and scope

Continued from `c800072fcd18e3aaef2ad8565c5960c659f2fb08` on the existing main checkout. CODEX-008 was still uncommitted. Its tracked patch, new source files and documentation were copied into ignored `work/codex008-preserved/` before work began; its test results and original verification report remain intact. No reset, discarded change or replacement of the matching implementation was used. Matching weights/rounding were exposed as reusable constants/helper without changing its results. CODEX-008's original report describes that earlier checkpoint; this report covers the combined final verification.

The pre-existing untracked `Career_Command_Centre/` folder is unrelated and excluded from staging. No new package, migration, configuration secret, paid AI/API/SaaS or mandatory external service was added. CODEX-010 was not started. The user explicitly authorized one combined commit named `feat(intelligence): add job matching and skills analytics` followed by a normal push to origin/main after both phases and combined verification; no further approval is required. The actual commit/push/remote CI result is recorded in the final task report, avoiding a self-referential commit hash in source.

## Delivered

CODEX-008: deterministic job/profile or selected-resume matching; explicit source separation; manual required/preferred and confirmed resume skill editing; score counts, weights, per-skill contributions, evidence timestamps, limits, concurrency checks and owner-scoped APIs. See [matching](resume-job-matching.md).

CODEX-009: protected `/skills` navigation and responsive page; scoped saved-job demand; assessed/unassessed job counts; aggregate profile matching coverage; three evidence statuses with explicit profile/resume flags; distinct learning/assessment and evidence-review priorities; parameterized search, allowlisted sorting, status/state filters and server pagination; lazy per-skill job lists with complete-job matching scores and links. No historical trend is invented. See [skills intelligence](skills-intelligence.md).

## Combined final local results

| Check                                                | Result                                                                                              |
| ---------------------------------------------------- | --------------------------------------------------------------------------------------------------- |
| Backend unit tests                                   | 104 passed, 0 skipped                                                                               |
| Integration suite with real PostgreSQL 17 containers | 104 passed, 0 skipped                                                                               |
| Frontend tests                                       | 98 passed                                                                                           |
| Locked .NET restore                                  | Passed                                                                                              |
| Release solution build                               | Passed, 0 warnings/errors                                                                           |
| Frontend production build                            | Passed, including `/skills`                                                                         |
| Lint and typecheck                                   | Passed                                                                                              |
| Prettier                                             | Passed with only the unrelated pre-existing untracked Career_Command_Centre folder excluded locally |
| git diff --check                                     | Passed                                                                                              |
| Compose validation                                   | Passed                                                                                              |

The complete backend solution and all frontend suites were run again with CODEX-008 and CODEX-009 together. Database tests use isolated disposable databases on a real PostgreSQL 17 Testcontainers server; no in-memory substitutes, skipped database checks or development-volume rollbacks.

New CODEX-009 unit tests cover evidence category/action decisions, required/preferred weighting, learning/evidence separation, rounding, empty denominators and bounded query validation. PostgreSQL tests cover exact aggregate counts/weighted coverage, explicit empty/unassessed states, fresh results after profile/resume/requirement changes, active/archived/all scope, filters/pagination, direct equality to CODEX-008 job matching scores, two-user shared-vocabulary isolation, foreign skill drilldowns, anonymous access, malformed/invalid query bounds, no-store and metadata confidentiality. A 100-requirement query-shape test verifies four analysis queries and three drilldown queries, projected columns, SQL aggregation and bounded pages. The initial EF nested-null projection issue and unnecessary grouped-join job columns were fixed and the full suite rerun.

New frontend tests cover summary/source distinctions, true unknown coverage, missing/one-source/supported actions, explainable formulas, filter/search/scope requests, lazy job expansion and both pagination flows, empty filtered results, retries, per-skill errors and ignoring obsolete requests after scope changes. Navigation is covered by the existing shell test. Existing CODEX-008 matching, document, auth, opportunities, profile and applications regressions remain in the same passing suites.

## Browser and local services

The actual production frontend and Release API were started with existing local configuration. API readiness reports PostgreSQL and Redis Healthy. No storage or existing user records were modified during CODEX-009 browser checks. The existing CODEX-008 local verification job supplies real saved requirements to the preview; no fake profile/resume ability claim was introduced to make the page look populated. Its 0% profile coverage is truthful.

Verified in the authenticated browser: page and active navigation; real summary; per-skill job expansion and full-job score/link; supported-status filter returning an honest empty state; archived scope with no jobs showing Not scored; filter reset; mobile job expansion; responsive desktop, 768×1024 tablet and 390×844 mobile. No horizontal overflow at checked sizes. Browser screenshots saved separately in task outputs. Temporary viewport overrides are reset and the Skills intelligence preview is left open at `http://localhost:3000/skills`. Browser console error review and final readiness checks are recorded in the final task report.

## CI and limits

Existing GitHub Actions automatically discovers the added tests and runs locked restores, formatting/lint/typecheck/build/tests plus Compose. The local excluded folder is untracked and absent from CI, so CI's ordinary full-repository format check applies. The workflow remains unchanged and no checks are weakened. The new remote run is checked after the authorized combined push; its exact commit, job outcomes and URL belong in the final task report.

Coverage/priorities are current personal-record heuristics, not market statistics, hiring predictions or verified ability. Requirements and evidence are manual; no PDF/prose parsing, aliases, proficiency inference or historical snapshots. Profile-only and resume-only skills prompt evidence/profile review, not learning claims. Aggregate coverage is weighted requirement-point coverage, not mean job coverage. Results are consistent per-response snapshots; reload after editing elsewhere. Read queries remain four/three commands independent of row count, but aggregate database work still scales with the owner's requirement associations. Further history, proficiency modeling and wider analytics remain separate scope. The next numbered phase is CODEX-010; no implementation was started.
