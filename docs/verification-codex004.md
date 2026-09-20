# CODEX-004 verification

Verified September 20, 2026 on Windows with .NET SDK 8.0.425, Node 24.14.0, npm 11.9.0, Docker Engine 29.8.0 and real PostgreSQL 17 containers.

## Results

| Check                        | Result                                                                                                                                                          |
| ---------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Backend Release build        | Passed, zero warnings/errors                                                                                                                                    |
| Backend unit tests           | 48 passed, none skipped                                                                                                                                         |
| Backend integration tests    | 74 passed, none skipped, RUN_CONTAINER_TESTS=true                                                                                                               |
| Migration chain              | Clean apply, upgrade from AuthenticationFoundation with an existing profile, rollback to AuthenticationFoundation, rollback to zero, and reapplication passed   |
| Database constraints/indexes | Existing suite retained; preference enum check and varchar_pattern_ops index verified                                                                           |
| Model/snapshot comparison    | No pending model changes                                                                                                                                        |
| Idempotent SQL               | Generated and reviewed in ignored artifacts/CareerProfilePreferences.sql                                                                                        |
| Frontend lint                | Passed, zero warnings                                                                                                                                           |
| Frontend typecheck           | Passed                                                                                                                                                          |
| Frontend tests               | 36 passed across six files                                                                                                                                      |
| Frontend production build    | Passed; profile/settings and workspace protected routes render dynamically                                                                                      |
| Compose                      | Configuration validation passed                                                                                                                                 |
| Source/security/cost review  | Completed; findings and corrections below                                                                                                                       |
| Browser                      | Registration/login/logout, profile/preferences persistence, skill add/edit/remove/search, account rename, protected routing and responsive inspection completed |

CODEX-001/002/003 tests remain in the suite. The existing migration-count assertion now expects three migrations while retaining all rollback/constraint/concurrency checks. Earlier migration files were not changed. No CI weakening or workflow change was needed.

## New test coverage

Seventeen additional unit cases cover profile boundaries, preference normalization, invalid commands leaving aggregates unchanged, enum membership, skill normalization/experience and account-name validation.

Thirteen additional real PostgreSQL scenarios cover anonymous rejection on every product route; empty and legacy missing-profile behavior; normalized persistence and server timestamps; two-user isolation; ignored foreign query selectors and rejected body mass assignment; stale/simultaneous profile saves; invalid requests and CSRF; canonical reuse and concurrent creation; owned relation update/delete isolation; literal, bounded skill search; account rename; password-change regression; and preference migration upgrade/rollback/reapply. Existing authentication and full ownership-graph tests also pass.

Fifteen additional frontend cases cover profile load/empty/retry, experience/preferences validation, successful saves and version submission, retained drafts after conflicts, discard, skill add/remove/update and deletion failure, safe account rendering, display-name validation/editing, password validation/success/failure, and auth-state clearing with password-change feedback. Existing route, auth and API-client tests remain green.

## Browser verification

A disposable PostgreSQL container used loopback port 55440, random process-only credentials and a separate careeros_browser database. A temporary API ran on 5082 and Next development server on 3001. A clearly named test account was registered through the real UI; this data was not seeded into application code or a user database.

Executed UI checks:

- Register, sign out, navigate to /profile while anonymous and observe /login, then log in and observe Overview.
- Save career information, target roles/locations, work mode, employment/seniority preferences; reload and confirm persistence.
- Add PostgreSQL, edit proficiency/experience, reload and confirm persistence, remove the relation, find the retained canonical skill through search, and add it again.
- Rename the account through Settings and observe the real updated account name, including after a new login.
- Inspect profile and settings at desktop 1440×1000, tablet 820×1180 and mobile 390×844. DOM width checks showed no horizontal overflow; mobile preference controls and skill metadata wrap correctly.
- Open mobile navigation, verify account controls and sign out successfully.
- Submit an empty password form and observe validation. Browser console inspection returned no warnings/errors for the inspected session.

Live password-change submission was not performed: the browser tool's credential-change policy requires user handoff before entering a new credential. No bypass or alternative UI automation was used. The existing password-change endpoint, frontend submission/clearing and session invalidation are covered by the executed automated tests. Other browser interaction used the real API/database, not mocked responses.

Temporary verification servers stopped; their ports had no remaining listeners when checked. The named disposable PostgreSQL container and its anonymous volume were removed. Testcontainers disposed its resources. No running Docker container remained. The original localhost:3000 user server was not stopped or replaced. Next dev generated AGENTS.md/CLAUDE.md locally during verification; those new tool-generated files were removed after the dev server stopped and are not phase changes.

## Review findings and corrections

- Unknown JSON fields initially surfaced as 500 through the existing global handler in Development. Safe BadHttpRequestException mapping now returns generic 400/413 without request content. Regression tests reject UserId, email and audit-field mass assignment.
- PostgreSQL timestamps have microsecond precision; the new test's comparison was corrected to that precision rather than weakening server-managed audit behavior.
- Profile saves compare incoming xmin and use EF concurrency at save time; simultaneous saves prove one winner and one 409. Skill metadata/deletion use the same mechanism.
- Canonical creation uses parameterized INSERT ON CONFLICT plus the existing unique generated normalization column; a concurrent two-user test proves one canonical row and two owned relations.
- Account responses expose only existing safe account fields and an edit version. No credential hash/stamp/token fields were added to product DTOs.
- Auth and ordinary request bodies retain the previous 16 KiB limit; only PUT /profile receives 128 KiB for bounded Unicode summaries/preferences. Cookie, CORS, CSRF, JWT/session checks and auth rate limits remain intact.
- Password change checks/refreshes the session before calling the existing credential mutation without retrying invalid current passwords. Success clears temporary passwords/live account state and redirects to login; no browser credential storage was introduced.
- Mobile inspection found sign-out hidden with the desktop header. The mobile navigation now includes actual account/sign-out controls.
- Profile/settings retain the established design tokens, accessible native controls and honest empty states. No fabricated profile, skill or dashboard data appears in production source.
- Source searches found no unfinished TODO/FIXME implementation markers, real secret patterns, hard-coded authenticated user, token logging, plaintext persistent security tokens or localStorage/sessionStorage use in the new feature. Search mentions in historical verification reports are documentation, not unfinished work.
- One transient Windows file-write error during formatting and one strict TypeScript test-tuple error were corrected; subsequent checks passed. No tests, limits or validation were disabled to obtain green results.

## Cost review

No npm/NuGet dependency or lockfile changed. No paid API, external skill service, SaaS SDK, billing integration, cloud service, object store, AI provider or deployment was added. Skill search and all product data use existing PostgreSQL. Browser/API requests remain same-origin/first-party. Existing local Redis configuration is unchanged. The permanent zero-cost requirement is documented in README, architecture and profile guidance.

## Executed commands

```sh
git status --short --branch
git log -4 --oneline
dotnet build apps/api/CareerOS.sln --configuration Release --no-restore
dotnet test apps/api/CareerOS.sln --configuration Release --no-build --logger "trx;LogFileName=codex-004.trx"
dotnet ef migrations add CareerProfilePreferences --project apps/api/src/CareerOS.Infrastructure --startup-project apps/api/src/CareerOS.Api --output-dir Persistence/Migrations --configuration Release
dotnet ef database update --project apps/api/src/CareerOS.Infrastructure --startup-project apps/api/src/CareerOS.Api --configuration Release --no-build
dotnet ef migrations has-pending-model-changes --project apps/api/src/CareerOS.Infrastructure --startup-project apps/api/src/CareerOS.Api --configuration Release --no-build
dotnet ef migrations script --idempotent --project apps/api/src/CareerOS.Infrastructure --startup-project apps/api/src/CareerOS.Api --configuration Release --no-build --output artifacts/CareerProfilePreferences.sql
dotnet format apps/api/CareerOS.sln --no-restore
npm run lint
npm run typecheck
npm test
npm run build
npm run format
docker compose --env-file .env.example config --quiet
git diff --check
```

Database tests used RUN_CONTAINER_TESTS=true. The web production build used nonsecret API_INTERNAL_URL=http://localhost:5080. Tooling JWT keys and disposable database credentials were generated in process memory and never committed. Final formatting/status checks, the single commit, push and GitHub CI outcome are reported after this pre-commit verification record.

## Known limits and deferred features

One preferred work mode is retained; no additional remote boolean is needed. Canonical skills remain printable ASCII without aliases/localization. Shared skill names are vocabulary visible to authenticated users, so they must not contain private details. Product drafts are in memory, warn on unload, and are not autosaved or preserved on client-side navigation; concurrent edits require deliberate reload/reconciliation. Large skill collections have no pagination yet; canonical search is always bounded to ten.

Email change, verification delivery, production recovery delivery, other CRUD, application pipeline, uploads/storage, scraping, AI/embeddings, analytics, social login, MFA, billing/subscriptions and deployment remain deferred. Existing CODEX-003 operational limits remain documented in authentication.md. The recommended next product phase is a small owner-scoped company/opportunity workflow, retaining the zero-cost requirement.
