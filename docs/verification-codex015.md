# CODEX-015 final release verification

Verified locally on 2026-10-17, starting from `dbe24c7597c8fd5721b9f5e3157e79825e43822b` plus all existing uncommitted CODEX-014 work. No reset/discard, user-data migration, paid service, public hosting or model installation. Unrelated `Career_Command_Centre/` is excluded from this release.

## Delivered

Replaced stale phase-by-phase README status with current features, architecture diagram, exact stack, Windows/Docker quickstart, privacy, limitations, troubleshooting and verification commands. Added configuration placeholders, safe operations/upgrade/recovery guidance, synthetic demo walkthrough with real screenshots, and evidence-bounded English/Turkish CV/LinkedIn copy. Existing feature contracts remain linked. Local AI stays disabled. Five existing migrations remain unchanged.

CODEX-014 authentication-return, skill-batching, date guidance and dependency changes are preserved. Applied whole-solution C# formatting to resolve previous violations; added CI formatting verification. Compose CI now starts clean services and probes PostgreSQL/Redis, in addition to parsing configuration. No tests were disabled or failure thresholds weakened.

## Local evidence

| Check                                      | Result                                                                                                                                               |
| ------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| Locked npm install                         | Passed after stopping the project's web process to release Windows native-module lock                                                                |
| Locked NuGet restore/local EF tool restore | Passed                                                                                                                                               |
| Backend Release solution build             | Passed, zero warnings/errors on final sequential run                                                                                                 |
| API Release publish                        | Passed to ignored local work/release-api                                                                                                             |
| Backend unit tests                         | 143 passed, zero skipped                                                                                                                             |
| Real PostgreSQL integration tests          | 122 passed, zero skipped; Testcontainers PostgreSQL 17                                                                                               |
| Frontend tests                             | 143 passed, 19 files                                                                                                                                 |
| Frontend lint/typecheck/optimized build    | Passed                                                                                                                                               |
| Whole-solution .NET format verify          | Passed; prior whitespace violations resolved                                                                                                         |
| Live HTTP smoke                            | Three passed: anonymous/invalid-cookie deep links and hostile-origin CSRF through real Next proxy                                                    |
| Browser smoke                              | Five passed using existing synthetic interview: recovery/query, date reload/UTC, invalid interval rejection, corrected save and unsafe return target |
| Browser product views                      | Overview, pipeline, interview, analytics and disabled AI inspected; real screenshots saved                                                           |
| Clean Compose                              | Separate careeros-release-check project, new volumes, ports 55432/56379; PostgreSQL 17.11 healthy and SELECT version succeeded; Redis healthy/PONG   |
| Clean schema setup                         | All five EF migrations applied to the separate empty database; pending-model check reports none                                                      |
| Paired restore                             | Included in 122-test suite: real pg_dump/pg_restore, private PDF bytes/hashes, reauthentication and other-owner denial                               |
| Production npm audit                       | Zero known vulnerabilities                                                                                                                           |
| Full npm audit                             | Five high development dependency entries for one underlying braces advisory; see below                                                               |
| NuGet direct/transitive vulnerability scan | No vulnerable packages reported by configured sources                                                                                                |

Initial Windows attempts encountered native-module/API binary locks; a mistakenly overlapping solution build also encountered the active testhost lock. Processes were sequenced and final build/format completed cleanly. Failed attempts are not counted as successful verification. API/web were restarted using the new optimized/published artifacts; PostgreSQL/Redis user volumes remained intact.

Prettier whole-repository check and Git whitespace check passed. Final local readiness returned HTTP 200 with PostgreSQL and Redis healthy. Origin/main was fetched and matched the baseline before staging. Exact commit and remote CI evidence are recorded in the completion report after publication. This committed document deliberately does not predict the hash or CI outcome of its own future commit.

## Fresh synthetic benchmark

Raw ten-call samples: [read workloads](performance-codex015.json), [skill writes](skill-write-codex015.json). Methodology/fixture shape and caveats are unchanged from [CODEX-014](performance-codex014.md): two owners, 100/1,000/5,000 companies/jobs/applications/tasks/interviews per owner; warm sequential in-process services plus real PostgreSQL, no HTTP/auth/render time. Local host/build contention affects timing. Ten samples cannot establish reliable p95/throughput or an SLA.

| Records per owner | Dashboard median ms | Analytics median ms | Skills median ms | Tasks median ms |
| ----------------- | ------------------- | ------------------- | ---------------- | --------------- |
| 100               | 46.83               | 55.10               | 48.13            | 38.38           |
| 1,000             | 44.11               | 64.15               | 51.16            | 47.23           |
| 5,000             | 58.93               | 142.34              | 76.10            | 39.35           |

Skill replacement uses six commands at both 10 and 100 skills (single observations: 52.60/69.52 ms). The historical 204-to-six command comparison is structural evidence from CODEX-014; this release does not attribute timing differences to a new read optimization. Fixed query/response budgets and ownership assertions passed.

## Audits and residual limits

- Full npm audit still reports `braces <=3.0.3` through micromatch → fast-glob → Next ESLint tooling, five affected development entries. Registry `npm view braces version` returns 3.0.3. No patched compatible version was available at review. The suggested forced downgrade to Next ESLint 14 was rejected as incompatible with this Next 16 release. Do not process untrusted glob input in development tooling. Production audit is separately clean. [Advisory](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm).
- C# format debt is resolved for this revision and now gated in CI; no framework-major upgrade was included. [.NET 8 support ends 10 November 2026](https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/).
- No public deployment, TLS/ingress validation, paid/free cloud provisioning, production email adapter, antivirus, cumulative disk quota, distributed rate limiter, retention policy automation or complete nonce CSP is claimed. See [security review](security-codex014.md).
- No real Ollama model, model quality/hardware benchmark, physical-device suite, spoken screen-reader audit, formal WCAG certification, penetration test, operator-data restore or measured operational RPO/RTO. Browser checks use the desktop in-app browser and one synthetic account; they are not exhaustive E2E coverage.
- Clean installation was verified with new Docker infrastructure and migration chain on the existing Windows toolchain; a separate fresh OS installation/full application-container deployment was not performed.

## URLs and cost

Local web: http://localhost:3000/ · API readiness: http://localhost:5080/health · Development OpenAPI: http://localhost:5080/swagger/v1/swagger.json · Repository: https://github.com/Ekintaner/careeros · CI: https://github.com/Ekintaner/careeros/actions/workflows/ci.yml

No mandatory financial cost or paid API/service. Existing hardware/power/internet are operator costs. Free-provider investigation found Render's ephemeral free filesystem and 30-day PostgreSQL expiry unsuitable as a durable complete default; [source and options](release-operations.md#deployment-options). No deployment is claimed.
