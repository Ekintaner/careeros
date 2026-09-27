# CODEX-006 verification

Executed September 27, 2026 on Windows with .NET SDK 8.0.425, Node 24, Docker and real PostgreSQL 17. This report records local verification before the single phase commit. The final task response records the resulting commit, push and CI run.

## Results

| Check                       | Result                                                                                                                |
| --------------------------- | --------------------------------------------------------------------------------------------------------------------- |
| Backend Release build       | Passed, zero warnings/errors                                                                                          |
| Backend unit tests          | 74 passed, none skipped                                                                                               |
| Backend integration tests   | 91 passed, none skipped; RUN_CONTAINER_TESTS=true                                                                     |
| Frontend tests              | 71 passed in eight files                                                                                              |
| Frontend lint and typecheck | Passed                                                                                                                |
| Frontend production build   | Passed; four protected application routes included                                                                    |
| EF model comparison         | No pending model changes                                                                                              |
| Migration lifecycle         | Clean/full-chain apply, compatible rollback/reapply, and refusal to rewrite incompatible Applied-first history passed |
| SQL script                  | Idempotent migration script generated and reviewed in ignored artifacts/ApplicationTrackingLifecycle.sql              |
| Compose validation          | Passed using .env.example                                                                                             |
| Runtime readiness           | PostgreSQL and Redis healthy; API /health returns Healthy                                                             |
| Browser                     | Real login, creation, pipeline movement, history, metadata, notes, filtering and archive/restore verified             |
| Responsive inspection       | Desktop 1440×1000, tablet 820×1180, mobile 390×844; no horizontal page overflow                                       |

The complete existing auth/profile/company/job and relational suites remain enabled. Dependency manifests, lockfiles and CI workflow are unchanged. The migration-count assertion now expects five migrations; the table count remains 19. No earlier migration or persisted enum value was rewritten.

## Coverage

Ten additional unit cases exercise Saved/Applied initial history, invalid initial stages, backward transitions, preserved first applied date, terminal/archive/restore behavior, foreign/stale restoration history, explicit date corrections, plain-text note boundaries and query validation.

Eight additional PostgreSQL scenarios cover the requested application behavior in grouped workflows: owned-job creation and duplicate conflicts; genuine initial history and safe detail projection; persisted metadata; all filters, sorts, pagination and aggregation; ordered transitions, invalid/terminal moves and first-set date; simultaneous/stale mutation conflicts with atomic history; archive/restore; notes create/edit/delete and xmin conflicts; Unicode note size bounds; anonymous/CSRF/mass-assignment rejection; complete two-user application/history/note isolation; parent-note mismatch rejection; owned/foreign resume association; conservative delete; migration upgrade/rollback/reapply and incompatible downgrade rejection; and actual executed SQL query-count/projection checks. Existing tests independently verify composite ownership constraints, indexes, history immutability and restrictive relationship deletion.

Eighteen additional frontend tests cover honest empty state, server counts independent of page size, mobile stage selection, filters/archived view, accessible movement, failed moves, stale version recovery, Saved/Applied creation, existing-job selection, duplicate navigation, exact timestamp preservation, detail/history, plain-text notes, note CRUD/version handling, archive/restore, Job tracking links, safe missing-resource errors, note draft preservation across status refresh and note conflicts.

## Browser verification

Used only the existing local verification account/job and clearly labelled development data. Credentials and data are not committed. Signed out and signed in through the real UI, opened Applications, verified the empty five-stage board, then used Job → Track application to create Saved. Moved Saved → Applied through detail and Applied → Interview through the board. Actual counts and immutable initial/transition events changed accordingly. Entered a note draft before a status move, then successfully saved it afterward; edited the saved note and confirmed persistence. Edited application source on mobile while preserving its applied date. Archived/restored the application and confirmed Interview → Archived → Interview in history.

Search produced zero results and clearing it recovered the actual card. Tablet/mobile stage selection showed a usable vertical list. Desktop displayed five columns. Width measurements were at or below the viewport for board, tablet detail, mobile detail and mobile editor. Console inspection after verification reported no warnings/errors. No mocked network responses were used. Browser automation needed keyboard select-all/backspace to reliably clear a controlled search input; the resulting real query and recovery were verified.

The remaining local test application is in Interview. It is development database content, not a seed or production example. Preview is left on /applications.

## Review and corrections

- EF could not translate an intermediate positional-record projection. Replaced it with a composable member-initialized projection; real SQL tests now verify two bounded list queries and one summary GROUP BY. No in-memory fallback was introduced.
- Detail refresh originally risked unmounting an unfinished note draft. Detail now updates application state without unmounting notes; a regression test verifies preservation.
- Desktop card action wrapping was corrected with a nonshrinking Move button and shorter visible label while retaining the descriptive accessible name.
- A shared mobile badge rule hid status; application detail now uses a dedicated visible status label, verified on mobile.
- Existing job tests required a distinct application-lookup response after adding the tracking link. Original archive assertions are retained.
- One local concurrent run timed out across several existing/new frontend files. Separate reruns passed with unchanged timeout limits and all 71 cases enabled.
- The first Release build encountered the running API's file lock. Stopped that verified local process, rebuilt successfully and restarted it after backend verification.

Ownership/security review: every application, job/company join, resume, history and note query is owner-scoped; no browser UserId authority, EF entity serialization, raw dynamic SQL, HTML interpretation, credential persistence, external URL fetching or development identity bypass was added. Request fields are allowlisted. CSRF/auth guards and safe production errors remain active. xmin and unique constraints protect races; status/history writes are atomic. Notes are bounded and timestamps are server-owned. Source searches found no implementation TODO/FIXME, real secret patterns, hard-coded production user, new browser token storage or fake production records. Historical verification documents naturally contain those search terms.

Zero-cost review: no new package, paid/usage-billed integration, external workflow/search/analytics service, hosted drag/drop dependency, cloud resource or deployment. Core tracking uses the existing local/self-hostable stack.

## Local runtime

- Frontend: http://localhost:3000/applications
- API: http://localhost:5080; readiness: http://localhost:5080/health
- Existing Compose PostgreSQL and Redis remain running on loopback ports 5432 and 6379.
- API and Next development processes remain running with ignored logs under artifacts/preview-\*.
- Restart if the machine/session stops them: `docker compose up -d --wait`, `dotnet run --project apps/api/src/CareerOS.Api --configuration Release --no-build`, and `npm run dev` from the repository root. Existing ignored local environment and user-secrets are required; no credentials are reproduced here.

## Limitations and deferred scope

Explicit native status controls are the deliberate drag/drop alternative. Terminal outcomes cannot be reopened; archive/restore preserves them. Hard delete is intentionally unavailable. Applied-date corrections are explicit metadata and do not alter event history. Downgrading the migration is intentionally blocked when direct-Applied events would violate the old schema. Separate card/count requests can briefly differ during concurrent activity; Refresh reconciles them. Comprehensive internal-navigation draft blocking, cross-tab product cache synchronization and large-dataset search optimizations remain future work.

Resume association uses existing metadata only; uploads, document storage, interview/task management, notifications, AI/matching, scraping/import, analytics dashboards, billing and deployment are deferred. Recommended next task: secure local-first resume/document handling with metadata, ownership and zero-cost storage design.
