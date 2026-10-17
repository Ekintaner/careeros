# CODEX-013 verification — combined with CODEX-012

## Scope and preservation

Baseline: 6990b596590971073ac4f949a8b63c34df373abc. All uncommitted CODEX-012 assistance work is included unchanged in purpose: optional, disabled-by-default, loopback-only Ollama; grounded source excerpts and evidence-based suggestion templates; explicit consent; no cloud fallback, PDF content upload, model download or paid dependency. See verification-codex012.md and local-ai-assistance.md for the security tests and limitations. The later CODEX-013 request explicitly authorizes one combined commit and push; it supersedes the earlier approval hold recorded in the historical CODEX-012 report.

## Product changes

- Real Overview replaces the static welcome screen. One authenticated dashboard request returns owner-scoped PostgreSQL counts and bounded recent applications, next interviews and follow-ups. Seven fixed queries run in a repeatable-read transaction; each list is limited to five. Empty/error/loading states do not invent statistics.
- Shared product styles unify neutral green/gray colors, typography, spacing, page widths, buttons, fields, tables, badges and feedback. Inputs have stronger borders, controls have 44px minimum targets, focus rings are visible, and reduced-motion preferences are respected.
- Mobile navigation closes after choosing a destination. Compact Kanban mounts only the selected column while retaining every permitted stage in its selector; desktop keeps an internally scrollable board.
- Document deletion uses a native modal dialog with explicit irreversible-action text, Cancel focus, Escape handling, busy protection and focus restoration. Failed deletion remains visible in the dialog without duplicate live errors.
- Shared form fields associate hints/errors with native inputs and expose invalid state. Existing validation and user-entered values remain intact.
- No package, lockfile, migration, schema, matching score or analytics-definition change.

## Dashboard definitions

All counts describe current owned records, not inferred historical activity. Total applications includes all statuses and archived records. Active means Saved, Applied, Assessment, Interview or Offer. Offers means current Offer. Saved jobs means unarchived job records. Open tasks have no completed timestamp; overdue means due strictly before the UTC snapshot instant. Upcoming interviews have end strictly after that instant, including meetings already in progress. Dates render in the explicitly labelled device timezone. These convenience counts do not replace the cohort/conversion definitions on Analytics.

## Verification executed

| Check                                           | Result                                                                                                                             |
| ----------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------- |
| Backend Release unit tests                      | 142 passed; zero skipped                                                                                                           |
| Real PostgreSQL integration tests               | 118 passed; zero skipped                                                                                                           |
| Frontend tests                                  | 124 passed                                                                                                                         |
| Locked restore / backend Release build          | Passed                                                                                                                             |
| Frontend lint / typecheck / production build    | Passed                                                                                                                             |
| Formatting / whitespace / Compose configuration | Passed                                                                                                                             |
| Security regressions                            | Assistance ownership, CSRF, anonymous access, quotas, transport/grounding and dashboard owner isolation included in passing suites |

Dashboard integration verifies two-owner isolation, true counts, bounded lists, anonymous access and time boundaries. Frontend additions cover real data/error/empty Overview, dialog focus/cancellation, field descriptions, navigation closing and compact Kanban stage selection. The existing full suites cover scheduling/date validation, task lifecycle, matching, skills, analytics, documents and authentication.

## Screen and responsive audit

Desktop (approximately 1110px), tablet 768x1024 and mobile 390x844: 20 workspace routes at each width, 60 DOM probes. No horizontal document overflow or unlabelled native controls in those probes. Routes: Overview; profile; settings; company list/new; job list/new/detail/edit; application pipeline/new; resumes; documents; Skills Intelligence; interview list/new; task list/new; analytics; optional assistance. Loading-only probes were supplemented by populated dashboard, pipeline, document and AI observations. Login, register, forgot-password and reset-password layouts were also inspected signed out; no credential reset or new registration was submitted. Source audit included existing matching, profile/skills and analytics state/rendering paths. This is not a claim that every possible detail record and dataset was visually exercised.

Actual local browser workflows used an isolated verification account: created a labelled application, moved Saved to Applied on mobile, uploaded a blank PDF as document and resume, assigned the resume, opened/cancelled deletion with Escape and confirmed trigger focus restoration, created a task, completed and reopened it, and observed its dashboard count. Mobile menu selection closes the menu. Optional AI remains visibly unavailable with setup/privacy information and no automatic inference. Existing deterministic matching is retained; the blank resume contains no skill or career claims.

Synthetic records are clearly named CODEX-013 UI fixtures, not real submissions or achievements. The earlier CODEX-012 company/job fixture remains. No real candidate profile was populated. Deletion was not submitted. Interview scheduling was attempted with labelled synthetic fields; the browser automation did not reliably commit controlled datetime values and no interview was saved. Scheduling correctness is covered by the passing frontend/PostgreSQL suites, but a successful real-browser scheduling submission is not claimed.

Screenshots and the raw responsive probe JSON are supplied with the user-facing report outside the repository. Automated focus/label checks and keyboard observations improve accessibility; a formal WCAG certification, full screen-reader audit and physical mobile-device audit were not performed.

## Cost, privacy and limitations

No paid API, SaaS, UI library, hosted service or mandatory inference dependency was introduced. PostgreSQL, Redis and normal API/web remain the required local stack. AI stays disabled by default. No real Ollama daemon/model was installed or benchmarked; protocol behavior was tested with controlled handlers/providers. Model quality and hardware fit remain unmeasured. See local-ai-assistance.md for administrator trust and grounding limits.

Known pre-existing navigation limitation: an expired access session can refresh through /session and return to Overview instead of preserving the deep link. No authentication rule was loosened for UI polish. Dashboard aggregate work scales with the owner's records even though request count and returned lists are bounded. Very large datasets were not load-benchmarked. Native modal behavior assumes a modern browser.

## Deferred follow-ups (not implemented)

For a future CODEX-014 scope: preserve deep links through session refresh, complete a manual keyboard/screen-reader and real-device audit, and repeat interview datetime entry with a human-controlled date picker. For a future CODEX-015 scope: optional model/hardware evaluation with explicitly consented synthetic data, large-dataset performance measurement and further usability research. These are recommendations, not a change to the user's roadmap. Stop after combined submission and CI verification.
