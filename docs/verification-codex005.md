# CODEX-005 verification

Implementation and initial checks ran September 20, 2026; final review, builds and live browser verification resumed September 27 after an interruption. Windows, .NET SDK 8.0.425, Node 24, Docker Engine 29.8 and real PostgreSQL 17 were used.

## Results

| Check                      | Result                                                                                                                   |
| -------------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| Backend Release build      | Passed, zero warnings/errors                                                                                             |
| Backend unit tests         | 64 passed, none skipped                                                                                                  |
| Backend integration tests  | 83 passed, none skipped; RUN_CONTAINER_TESTS=true                                                                        |
| Frontend tests             | 53 passed across seven files                                                                                             |
| Frontend lint/typecheck    | Passed                                                                                                                   |
| Frontend production build  | Passed; all seven company/job routes included and protected                                                              |
| Migration model comparison | No pending changes                                                                                                       |
| Migration lifecycle        | Clean apply, existing-job upgrade from CareerProfilePreferences, rollback/reapply and full-chain rollback/reapply passed |
| SQL generation             | Idempotent migration SQL generated and reviewed in ignored artifacts/CompanyAndJobLifecycle.sql                          |
| Compose                    | Configuration validation passed; local PostgreSQL/Redis healthy                                                          |
| API readiness              | Healthy PostgreSQL and Redis at localhost:5080/health                                                                    |
| Browser                    | Real local API/database workflow and responsive inspection passed                                                        |

Existing CODEX-001 through CODEX-004 suites are retained. CI workflow and dependency lockfiles are unchanged. The migration-count assertion now expects four; table count remains 19. No earlier migration was changed.

## Coverage and review

Sixteen additional backend unit cases cover company name/length/URL validation, non-partial mutation, job validation, salary bounds/precision/metadata, UTC posting windows, query bounds/enum/sort checks, archive/restore idempotency and company owner protection.

Nine additional PostgreSQL scenarios cover anonymous routes, mass assignment and CSRF; company CRUD/duplicates/search/filter/pagination/audits; job CRUD/details/all filters/sorts/pagination; archive/restore; stale and simultaneous edits; two-user reads/updates/deletes/list isolation and foreign company rejection; restrictive deletion with actual application history; migration preservation/rollback/reapply; and executed SQL projection/query-count checks. Existing relational ownership tests independently exercise composite foreign-key protection. Each test database is isolated and disposable.

Seventeen additional frontend cases cover empty/loading/list/retry states, company create/edit/delete/conflict/cancel, server query submission, required owned company selection, job creation/editing/validation, duplicate submission prevention, job details/safe external links, archive behavior, delete conflicts and preserving unchanged timestamp precision. Tests do not assert visual styling.

Security and query review found no new owner bypass, browser-supplied UserId authority, unrestricted company lookup, entity serialization, raw dynamic SQL, HTML injection or server-side URL fetching. OwnedData scopes company joins/counts as well as resources. DTO allowlists reject unknown JSON fields. Database RESTRICT protects deletion races; xmin protects edits/deletes/archive. Each list uses a SQL count plus bounded projection; no descriptions or per-row database calls. Existing indexes are retained, with no speculative new infrastructure.

Source searches found no unfinished TODO/FIXME markers in production code, real secret patterns, hard-coded production users, new localStorage/sessionStorage use, permissive CORS, plaintext persistent security tokens or token logging. Test fixtures contain intentionally synthetic records; the browser test account/data exist only in the local development database. No credentials or local configuration are committed.

Corrections during verification:

- PostgreSQL truncates timestamps to microsecond precision. Persistence assertions now compare at that precision while preserving checks of fields and server audit behavior.
- Accessible company action names initially ran words together; explicit action labels now identify the company.
- An unrelated job edit initially rounded posting timestamps to minutes. Unchanged timestamps now retain original precision, with a regression test.
- Company job counts include archived records; count links now select all jobs.
- Local Redis readiness failed with localhost resolving outside Compose's IPv4 loopback binding. Development user-secrets now use 127.0.0.1:6379; the API and both dependencies report healthy. No production configuration was weakened.

## Live browser verification

A clearly named local verification account was created through the real registration UI, then signed out and signed back in. A company and job were created through their forms, edited and read back. The job included owned company selection, work/employment/seniority, source URL, description and a EUR monthly salary range. Archive and restore both changed the available action correctly. Company search returned an empty result and recovered after clearing; job search plus work-mode filters returned zero or one matching row as expected.

Inspected desktop 1440×1000, tablet 820×1180 and mobile 390×844. Companies, Jobs, details and forms retained readable controls and appropriate wrapping. DOM width checks showed no horizontal overflow. Mobile navigation exposed Companies/Jobs/account controls. Original-posting link attributes were verified by automated tests and the browser displayed the entered URL; no external page was fetched as part of import. Console inspection returned no warnings/errors. Browser actions used no mocked API responses.

## Cost and dependencies

No npm/NuGet package, paid API, usage-metered service, external job API, search service, enrichment service, SaaS account or mandatory cloud dependency was introduced. All product data is manually entered and stored in PostgreSQL. Redis remains the existing local dependency. No infrastructure was provisioned outside the user's computer, and no production deployment occurred.

## Local preview and commands

Normal Compose infrastructure runs on loopback PostgreSQL 5432 and Redis 6379 with persistent development volumes. Frontend: http://localhost:3000. API: http://localhost:5080. Development-only database/JWT credentials are stored in ignored .env and .NET user-secrets; apps/web/.env.local contains only the server-side API origin. Next-generated AGENTS.md/CLAUDE.md are locally excluded from Git rather than committed as feature changes.

The development processes are intended to remain running for the preview. Their final status, commit, push and CI outcome are checked after this pre-commit report. If the host restarts, run from the repository root (API and frontend in separate terminals):

```powershell
docker compose up -d --wait
dotnet run --project apps/api/src/CareerOS.Api
```

```powershell
npm run dev
```

Other executed verification commands:

```sh
dotnet build apps/api/CareerOS.sln --configuration Release --no-restore
dotnet test apps/api/CareerOS.sln --configuration Release --no-build --logger "trx;LogFileName=codex-005.trx"
dotnet format apps/api/CareerOS.sln --no-restore
dotnet ef migrations add CompanyAndJobLifecycle --project apps/api/src/CareerOS.Infrastructure --startup-project apps/api/src/CareerOS.Api --configuration Release --output-dir Persistence/Migrations
dotnet ef database update --project apps/api/src/CareerOS.Infrastructure --startup-project apps/api/src/CareerOS.Api --configuration Release --no-build
dotnet ef migrations has-pending-model-changes --project apps/api/src/CareerOS.Infrastructure --startup-project apps/api/src/CareerOS.Api --configuration Release --no-build
dotnet ef migrations script --idempotent --project apps/api/src/CareerOS.Infrastructure --startup-project apps/api/src/CareerOS.Api --configuration Release --no-build --output artifacts/CompanyAndJobLifecycle.sql
npm run format
npm run lint
npm run typecheck
npm test
npm run build
docker compose --env-file .env.example config --quiet
git diff --check
```

## Known limitations and next phase

Search is owner-scoped PostgreSQL substring matching, not a linguistic/full-text engine. Pagination totals can shift during concurrent changes. List filters/drafts are page-local; autosave/offline drafts and saved queries are not implemented. The company selector shows at most 20 matches and asks the user to refine search. Company duplicate suggestions and ISO currency membership validation are deferred; legitimate duplicate names remain allowed. Original source links are metadata only. Existing authentication operational limitations remain documented in authentication.md.

CODEX-006 should add owner-scoped JobApplication management and application history/pipeline UX using these real opportunities. Uploads, scraping/import, third-party integrations, AI/matching, analytics, notifications delivery, billing and deployment remain outside CODEX-005.
