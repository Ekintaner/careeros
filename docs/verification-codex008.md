# CODEX-008 verification

Implemented in the existing CareerOS checkout from `c800072fcd18e3aaef2ad8565c5960c659f2fb08`. The pre-existing untracked `Career_Command_Centre/` folder was left untouched. No commit or push has been made; explicit user authorization is required before either action. Work stops at CODEX-008.

## Delivered

- Pure deterministic `skills-v1` matching in Application, owner-scoped snapshot queries and atomic skill editors in Infrastructure, authenticated API routes and a responsive job detail panel.
- Manual Required/Preferred job skill sets and separately confirmed resume skill sets using the existing tables and canonical vocabulary. Job requirements do not become candidate claims. Uploads do not create skills. Profile and selected resume evidence are never merged.
- Weighted score, per-group counts and points, per-skill contributions, matched/missing evidence, dated source records, honest null-score and empty-source states, explicit limitations.
- Parent xmin protection for whole-set saves, rollback on failures, consistent snapshot reads, safe document lifecycle lock ordering, bounded validation and strict unknown JSON member rejection.
- No new migration, package, paid AI/API, mandatory external provider or configuration secret.

## Verification

Backend unit tests: **94 passed, 0 skipped**. PostgreSQL integration tests: **100 passed, 0 skipped**. Frontend tests: **86 passed**. The final backend run uses Release binaries and `RUN_CONTAINER_TESTS=true` with real disposable PostgreSQL 17 containers, not an in-memory substitute.

New coverage exercises deterministic weights/rounding/order, no requirements and empty evidence, punctuation/case identity, preferred-only scoring, name/enum/null/duplicate/count validation, profile/resume source separation, evidence removal and fresh recalculation, two-user isolation, anonymous access, CSRF, no-store/metadata confidentiality, stale and concurrent job/resume saves, no-op version advancement, injected write failure rollback, document deletion racing evidence edits, and constant 3/4-query matching with 100 requirements. Frontend coverage includes source switching, dated explanation, explicit resume confirmation, full-set requests/version, duplicate prevention, conflict draft preservation, read retries and older-resume pagination. Existing opportunity tests retain their focused feature mocks while the matching panel has its own contract suite.

Locked .NET restore, zero-warning Release build, frontend production build, lint, typecheck, Compose validation and `git diff --check` pass. Prettier passes for the repository while excluding only the pre-existing untracked `Career_Command_Centre/` directory through a temporary ignored work file; that unrelated folder is not part of the commit or CI checkout.

The existing `.github/workflows/ci.yml` automatically discovers the new xUnit/Vitest tests and already enables PostgreSQL container tests. Its restore/build/test/lint/typecheck/format/Compose steps were reproduced locally. **No new GitHub CI run is claimed:** a new remote run waits for an authorized commit/push.

## Live preview

API readiness reports PostgreSQL and Redis Healthy. The web app and API are left running at `http://localhost:3000` and `http://localhost:5080`. The browser preview is open on a separately created, explicitly labeled local verification job:

`http://localhost:3000/jobs/3663141c-3dc6-4d53-b6d4-7305f6b2356d`

In the existing local preview test account, the browser verified job creation, initial Not scored state, manual requirement editing, persistence and recalculated score, missing-evidence explanation and editor cancel/reload. No candidate skill claims were added to this account or its resumes. Its 0% result is truthful because those requirements have no recorded profile evidence. Resume confirmation/source/write behavior was exercised in dedicated frontend and real PostgreSQL integration tests.

Screenshots were captured at desktop and mobile sizes. Mobile (390×844), tablet (768×1024) and normal desktop views have no horizontal overflow; the mobile skill editor was also visually inspected. Browser console errors: none. Temporary viewport overrides were reset. Existing user records and files were preserved; the one new local preview job is clearly marked as a test fixture.

## Rules and limits

See [scoring and usage](resume-job-matching.md). Required weight 3; Preferred weight 1; score is earned/possible ×100, rounded to one decimal. Matching uses exact canonical IDs resolved through existing case normalization. Missing evidence is not a claim of missing ability. Manual self-assertions are not independent verification. No PDF parsing, OCR, aliases, inferred skills, proficiency/eligibility assessment, hiring prediction, historical score storage or external calls. Existing PDF storage/backup limits remain unchanged. Results are snapshots; reload after external edits. Skill saves advance parent versions, so other open job edits/actions may need their normal reload flow.
