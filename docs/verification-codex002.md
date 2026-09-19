# CODEX-002 verification

Verified on September 20, 2026 with .NET SDK 8.0.425, Node 24.14.0, npm 11.9.0 and real PostgreSQL 17.11 on Windows.

## Results

| Check                              | Result                                                                           |
| ---------------------------------- | -------------------------------------------------------------------------------- |
| Backend restore and locked restore | Passed, all six projects                                                         |
| Backend Release build              | Passed, zero warnings and errors                                                 |
| Unit tests                         | 18 passed, none skipped                                                          |
| Integration tests                  | 44 passed, none skipped: 38 real PostgreSQL cases and 6 existing HTTP cases      |
| Clean database migration           | Passed; 17 domain tables created                                                 |
| Schema/index/constraint inspection | Passed, including restricted FKs, primary-resume uniqueness and history ordering |
| Migration rollback                 | Passed; all 17 domain tables removed from a disposable database                  |
| Migration reapplication            | Passed; all 17 domain tables recreated                                           |
| Model/snapshot drift               | None; EF reports no pending model changes                                        |
| Idempotent SQL generation          | Passed; generated locally for review                                             |
| Frontend lint                      | Passed, zero warnings                                                            |
| Frontend type check                | Passed                                                                           |
| Frontend tests                     | 5 passed                                                                         |
| Frontend production build          | Passed                                                                           |
| C# formatting                      | Passed                                                                           |
| Frontend and CI source changes     | None                                                                             |

## Database test coverage

Real PostgreSQL tests exercise full graph persistence, generated email/skill normalization, profile/application uniqueness, primary-resume switching, every cross-owner dependent relationship, referenced-record deletion restrictions, salary/file/schedule/experience/enum checks, resume document type, authoritative sync/async audit timestamps, UTC round trips, xmin lost-update protection, atomic status/history writes and rejected stale status transitions.

The initial run found two test fixtures where uniqueness checks rejected a write before the ownership FK could be tested. The fixtures were corrected to use an unreferenced foreign document and a nonconflicting history sequence. Assertions still require PostgreSQL foreign-key violations; no constraint or test was weakened. All 11 cross-owner variants then passed, followed by the full 44-test integration suite.

## Commands executed

```sh
git status --short --branch
dotnet restore apps/api/CareerOS.sln
dotnet restore apps/api/CareerOS.sln --locked-mode
dotnet build apps/api/CareerOS.sln --configuration Release --no-restore
dotnet ef migrations add InitialDomainSchema --project apps/api/src/CareerOS.Infrastructure --startup-project apps/api/src/CareerOS.Api --output-dir Persistence/Migrations --configuration Release
dotnet ef migrations has-pending-model-changes --project apps/api/src/CareerOS.Infrastructure --startup-project apps/api/src/CareerOS.Api --configuration Release --no-build
dotnet ef migrations script --idempotent --project apps/api/src/CareerOS.Infrastructure --startup-project apps/api/src/CareerOS.Api --configuration Release --no-build --output artifacts/InitialDomainSchema.sql
dotnet test apps/api/CareerOS.sln --configuration Release --no-build --logger "trx;LogFileName=codex-002.trx"
dotnet format apps/api/CareerOS.sln --verify-no-changes --no-restore
npm run lint
npm run typecheck
npm test
npm run build
npm run format
npm run format:check
git diff --check
```

The initial unapplied migration was removed through EF tooling and regenerated after correcting the two notification indexes. The final migration is `20260919231621_InitialDomainSchema`; the migration/snapshot were not manually fabricated. Formatting commands were also run before verification.

Repository review and `rg` searches checked unfinished markers, credentials/token/private-key patterns, seed/fake-user behavior, frontend changes and migration contents. No real credentials or unfinished code markers were found in source. Verification documents mention the searches themselves. No new NuGet/npm dependencies were introduced; the unit project now explicitly references Domain.

## Local PostgreSQL environment

Docker's CLI is installed but its engine endpoint remains unavailable. To perform real verification before committing, official [EDB PostgreSQL Windows binaries](https://www.enterprisedb.com/download-postgresql-binaries) were downloaded into ignored local artifacts. A temporary PostgreSQL 17.11 cluster used loopback port 55439, SCRAM authentication and a randomly generated local password. The password was passed through the test process environment and never printed or committed.

`CAREEROS_TEST_POSTGRES` enabled the same database suite used by CI. The fixture created and removed only its own random databases, including the rollback database. This is real PostgreSQL testing, not an in-memory substitute. CI retains its existing `RUN_CONTAINER_TESTS=true` setup and uses PostgreSQL 17 Testcontainers. No Docker settings or system PostgreSQL service were installed or modified.

After testing, the temporary server was stopped and its plaintext password file removed. Automatic approval review blocked recursive cleanup of the remaining runtime directory; those files remain ignored under `artifacts/postgres-test` and are not part of the commit.

## Limitations and deferred work

Authentication, server-scoped queries, RLS, shared-skill administration, retention/erasure, file uploads and notification delivery remain unimplemented. No matching scores are persisted before a scoring/explanation contract exists. Canonical skills and account emails currently use deliberate ASCII normalization; localization/aliases require later design. No production database was touched. .NET 8 and ESLint compatibility limitations from CODEX-001 still apply.

The final task report records the single commit, push and CI outcomes after this local verification record was written.
