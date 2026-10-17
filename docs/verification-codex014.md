# CODEX-014 verification

Baseline `dbe24c7597c8fd5721b9f5e3157e79825e43822b`, verified 2026-10-17. Existing checkout and local data preserved. The unrelated untracked `Career_Command_Centre/` directory was left untouched. No commit, push, migration or CODEX-015 work.

## Delivered

- Server layout carries the real path/query into session recovery, with a proxy that overwrites forged routing headers. Recovery, login and mounted-workspace expiration use the same workspace-only return-target validator. External destinations and authentication loops fall back to Overview. A late account lookup cannot resurrect expired client state.
- Real native datetime picker opened, values entered, interview saved and reloaded. Local 2026-10-20 10:00–11:00 in Europe/Kiev round-trips as 07:00–08:00 UTC. Invalid end-before-start is rejected; correction saves. The new labelled synthetic interview is retained for repeatable regression checks; no invitation is sent. No actual date-control defect was reproduced. Field descriptions now explain timezone and end ordering, and authentication validation focuses the first invalid field.
- Canonical skill replacement reduced from 204 to six database commands at 100 skills. Shared vocabulary uses parameterized batches, and whole-set concurrency/rollback/ownership semantics remain. Two owners saving reversed overlapping 100-skill lists and SQL-shaped names pass.
- Next.js / eslint-config-next 16.3.8 plus patched sharp/source-map-js lockfile entries. Production dependency audit is clean; CI includes this gate. No paid dependency or mandatory AI service.
- Reproducible 100/1,000/5,000-record-per-owner PostgreSQL measurements, fixed query/response budgets and a real paired pg_dump/pg_restore/PDF restore test.

## Checks

| Check                                                              | Result                                                                                                                                                                                             |
| ------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Backend Release unit suite                                         | 143 passed, zero skipped                                                                                                                                                                           |
| Backend Release full integration suite before adding restore drill | 121 passed, zero skipped; real PostgreSQL 17 Testcontainers                                                                                                                                        |
| Added paired backup/restore drill                                  | 1 passed, zero skipped, Debug and Release; total current integration inventory 122                                                                                                                 |
| Frontend suite after dependency update                             | 143 passed across 19 files                                                                                                                                                                         |
| Real-browser regression helper                                     | Five scenarios passed on the patched production preview                                                                                                                                            |
| Actual Next/API HTTP checks                                        | Three passed: no-cookie and invalid-cookie deep links including forged header, and hostile-origin CSRF through the rewrite                                                                         |
| Locked npm install and NuGet restore                               | Passed                                                                                                                                                                                             |
| Backend Release solution build                                     | Passed, zero warnings/errors after releasing the running Windows API binary lock                                                                                                                   |
| Frontend lint, typecheck, production build                         | Passed with Next.js 16.3.8                                                                                                                                                                         |
| Production npm audit / NuGet advisory scan                         | Zero production npm vulnerabilities; no vulnerable NuGet direct/transitive packages reported by configured sources                                                                                 |
| Full npm audit                                                     | Five high development-tool findings from the unpatched braces dependency chain; documented in security review                                                                                      |
| Whole-solution .NET format verification                            | Failed on pre-existing whitespace/end-of-line violations, including untouched Analytics/Assistance/Planning files. This is not a CI gate. New test files formatted; no broad reformat was applied. |

Prettier, whitespace diff and Compose validation are part of the final local checks. An existing frontend pagination test was corrected to await the asynchronously loaded Next button; it continues to verify that pagination does not submit the task form. No failures are suppressed or tests disabled. The backup drill intentionally skips when tests use an externally supplied server: its pg_dump/pg_restore commands run only inside the disposable Testcontainers container. It ran locally with no skip.

GitHub CI for the uncommitted change cannot run without publishing Git state. The repository's CI-equivalent install/build/lint/typecheck/test/Compose checks were run locally; the old green GitHub run belongs to the baseline, not CODEX-014. The new browser/HTTP helpers are local live-preview checks, separate from the existing CI suite. No remote green result is claimed.

## Browser and accessibility scope

The real in-app browser used the already signed-in synthetic local verification account. Created fixture: `/interviews/96f12d99-9262-4324-9a75-17ad9223236d`, location `CODEX-014 synthetic browser verification`, linked to the existing synthetic application. Existing personal records were not edited. The browser checks cross the real production web build, API and development PostgreSQL. The benchmark and restore drill instead use disposable databases and private temporary roots.

`scripts/browser-hardening-checks.mjs` exports `verifyHardening(tab, options)` for a bound native accessibility-tree tab in the Codex browser JavaScript session. It requires an explicitly labelled synthetic fixture, the supplied interview path and the documented fixed test dates. It tests recovery target/query preservation, saved date/UTC conversion, invalid interval feedback, corrected save and an unsafe external destination. Run it after the current production build has loaded; stale client chunks after replacing a build may require a page reload. It does not sign out, create credentials or permanently delete records.

`scripts/http-hardening-checks.mjs` runs using Node's test runner:

```powershell
$env:CAREEROS_PREVIEW_URL = 'http://localhost:3000'
node --test scripts/http-hardening-checks.mjs
```

The initial native picker was expanded, Escape dismissed it, and Tab moved through the date's native segments. Accessibility-tree names expose Start, End, timezone and labelled form controls; frontend tests verify their accessible descriptions and validation focus. Native browser picker subcontrols are browser-owned. Spoken output with NVDA/JAWS/Narrator, a full keyboard route matrix and formal WCAG certification were not performed.

The Skip to content link was activated with Return; the native accessibility tree confirmed focus moved to main-content.

Desktop was visually checked on the new build. Responsive source rules and existing mobile navigation/compact Kanban tests were reviewed; this phase did not repeat CODEX-013's complete 60-view mobile/tablet layout probe. Physical devices, mobile native picker implementations and real assistive-technology combinations remain unverified. No claim of fresh full mobile coverage is made.

## Performance, security and cost

See [performance methodology and actual samples](performance-codex014.md) and [security findings/residual risks](security-codex014.md). Read benchmarks are in-process service+real database measurements, not HTTP/browser latency or load-test SLAs. Small sample sizes and local contention are disclosed. No real Ollama model was installed or benchmarked. Optional AI is still disabled by default, and every core feature remains available with no mandatory financial cost.

The local preview is `http://localhost:3000/`; API readiness is `http://localhost:5080/health`. PostgreSQL and Redis persistent volumes are retained. Services were briefly restarted for Windows binary locks and production build replacement, then restarted with the current code.
