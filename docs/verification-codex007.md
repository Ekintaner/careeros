# CODEX-007 verification

Implemented in the existing checkout at `25220978e519b3e271770f2b11df4e0505ce5fda`. The pre-existing untracked `Career_Command_Centre/` directory was left untouched. No new dependency, migration, paid API, hosted storage or mandatory service was introduced.

## Delivered

- Authenticated, owner-scoped raw PDF upload, paginated metadata list, attachment download and permanent deletion.
- Resume metadata created atomically with uploads; owner-serialized primary selection and existing application create/edit assignment.
- Restrictive deletion for every assigned resume, including archived applications; unassigned resume/skill/document metadata removed transactionally.
- Private local filesystem root outside the checkout and application directory, generated allowlisted keys, sanitized download filenames, MIME/header/trailer/extension and streaming 5 MiB validation, link rejection.
- Responsive `/documents` and `/resumes` screens with upload, filtering, pagination, metadata, primary actions, deletion confirmation, loading/empty/error/success states and accessible labels.
- Paired database/filesystem backup, restore and manual orphan reconciliation guidance in `docs/documents-and-resumes.md`.

## Validation

Final local results: 87 backend unit tests, 95 PostgreSQL integration tests and 77 frontend tests passed, with no skipped tests. Release/backend and frontend production builds, locked restore, lint, type checks, scoped formatting and Compose validation passed. Backend tests use real disposable PostgreSQL 17 containers; no in-memory database substitutes are used for document ownership or consistency checks. Coverage includes two-user isolation, anonymous access, CSRF, unsafe filenames, unsupported MIME/signatures, oversized streams, metadata confidentiality, byte-for-byte downloads, primary-selection concurrency, foreign resume assignment, assigned/archived deletion restrictions, injected database write/delete failures and missing files.

The existing CI workflow already enables all PostgreSQL tests with `RUN_CONTAINER_TESTS=true` and discovers the new tests without workflow changes. Local verification follows its restore/build/test/lint/typecheck/Compose steps. The previous CODEX-006 GitHub run remains successful: https://github.com/Ekintaner/careeros/actions/runs/36343722896 . That run verifies the previous phase. The user explicitly authorized committing and pushing CODEX-007 to origin/main; the new push triggers its own CI run, whose outcome must be checked separately.

The repository formatting check succeeds when excluding the pre-existing, untracked `Career_Command_Centre/` folder. An unqualified `npm run format:check` also scans that unrelated folder and reports 26 existing formatting issues; those files were not changed. `git diff --check` passes.

The actual browser preview was checked with its existing authenticated local test session on desktop. The responsive CSS includes single-column tablet/mobile layouts; separate physical mobile/browser viewport verification was not available. The filesystem symlink test executes its Unix branch on Linux CI; the local Windows suite exercises key/root/path guards without requiring symbolic-link privileges.

## Limits

PDF only, maximum 5 MiB. Signature validation is not antivirus scanning or full PDF structural validation. There is no PDF parsing, AI processing, inline preview or content indexing. PostgreSQL and local disk cannot share a distributed transaction: crashes or failed byte cleanup can leave inaccessible private orphan files; maintenance guidance explains reconciliation while writers are stopped. Storage needs a dedicated account, restrictive OS permissions, disk capacity and paired backups. Deployment across multiple API machines requires a shared private storage design; this phase targets one local/self-hosted API instance.

CODEX-008 and later phases were not started.
