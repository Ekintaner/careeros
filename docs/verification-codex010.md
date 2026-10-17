# CODEX-010 verification

Implemented locally against baseline 60201a7af9391ff10c16043b83678cab5b0f199b. No commit or push is authorized yet. CODEX-011 is not started.

| Check                                                  | Result                                                                                                                         |
| ------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------ |
| Locked .NET restore                                    | Passed                                                                                                                         |
| Release solution build                                 | Passed; zero warnings/errors                                                                                                   |
| Backend unit suite                                     | 110 passed; zero skipped                                                                                                       |
| Integration suite, actual PostgreSQL 17 Testcontainers | 110 passed; zero skipped                                                                                                       |
| Frontend suite                                         | 108 passed                                                                                                                     |
| Frontend lint and typecheck                            | Passed                                                                                                                         |
| Production frontend build                              | Passed with all six planning routes                                                                                            |
| Prettier                                               | Tracked repository files and all new frontend/docs files pass; unrelated pre-existing untracked Career_Command_Centre excluded |
| Compose configuration and git diff whitespace          | Passed                                                                                                                         |
| DST check in America/New_York                          | Spring gap and autumn repeated hour rejected; explicit UTC disambiguation passed                                               |
| Local readiness                                        | PostgreSQL and Redis healthy, API 5080 and frontend 3000 running                                                               |

New backend tests cover query bounds/views, domain types and note rules, interview and note CRUD/restricted deletion, Unicode note limits, task links/completion/reopening, UTC and URL validation, mass-assignment/CSRF/anonymous protection, two-user read/write/parent-child/link isolation, stale versions and real simultaneous edits with exactly one winner. Upcoming/past/overdue boundaries and bounded pagination are tested on PostgreSQL. The existing model snapshot remains unchanged with no pending schema changes.

New frontend tests cover real lists/links/priority/status, filters and pagination, empty/error/retry states, UTC/local conversion and invalid dates, create payloads, versioned editing/completion/reopening, conflict draft retention, note text escaping/editing and delete cancellation. A regression test proves application-picker pagination cannot submit the surrounding form.

Authenticated browser checks use the existing local verification account and its existing application. No appointments, notes, tasks or seed users are added to the development database. Verified interview list's honest empty state, schedule form and owned application selection, task form, mobile single-column controls and accessible labels. Desktop and 390×844 mobile checks show no horizontal overflow. Console warning/error inspection was empty. Browser timezone is Europe/Kiev. The preview is left open and viewport override reset after checks. CRUD and concurrent mutations run in disposable PostgreSQL test databases rather than altering existing personal records.

No new migration, package, external API, email/calendar integration or mandatory cost. Model limits remain documented: task status is Open/Completed; interview outcomes are plain text; overlaps are allowed; no reminders/recurrence/cancellation model or automatic pipeline transition. Minute-precision entry has an explicit UTC option for ambiguous local times.

GitHub REST inspection confirmed baseline CI run 39029653368 completed successfully for exactly 60201a7af9391ff10c16043b83678cab5b0f199b. The existing CI workflow automatically discovers the added tests and continues to run locked restore/build/unit/PostgreSQL/frontend/format/lint/typecheck/Compose checks. A remote CI result for CODEX-010 cannot exist until explicit commit/push approval and publishing; this is pending, not claimed green.

Follow-up: CODEX-011 was subsequently completed under the user's explicit authorization for one combined CODEX-010+011 commit/push. The original verification above records the pre-011 state; see [CODEX-011 verification](verification-codex011.md).
