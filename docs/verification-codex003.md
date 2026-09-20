# CODEX-003 verification

Verified September 20, 2026 on Windows with .NET SDK 8.0.425, Node 24.14.0, npm 11.9.0 and real PostgreSQL 17.11.

## Results

| Check                       | Result                                                                                                                 |
| --------------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| Locked NuGet restore        | Passed, six projects                                                                                                   |
| Backend Release build       | Passed, zero warnings/errors                                                                                           |
| Backend unit tests          | 31 passed, none skipped                                                                                                |
| Backend integration tests   | 61 passed, none skipped                                                                                                |
| PostgreSQL migrations       | Clean apply, initial-schema upgrade with existing user, rollback to initial, rollback to zero and reapplication passed |
| Model/snapshot comparison   | No pending changes                                                                                                     |
| Idempotent migration SQL    | Generated for review in ignored artifacts                                                                              |
| Frontend lint/typecheck     | Passed                                                                                                                 |
| Frontend tests              | 21 passed across five files                                                                                            |
| Frontend production build   | Passed with API_INTERNAL_URL configured; workspace dynamic, account pages static                                       |
| C#/Prettier formatting      | Passed                                                                                                                 |
| Compose validation          | Passed with .env.example                                                                                               |
| Diff/source/security review | Completed; no plaintext persistent security tokens or committed production secrets found                               |
| Live browser verification   | Not completed; see limitation below                                                                                    |

The original 18 unit tests, 44 integration tests and five frontend tests remain covered. Authentication adds 13 unit cases, 16 real-database HTTP scenarios and a full two-user ownership scenario; frontend adds 16 cases. Existing migration assertions now expect two migrations/19 tables and retain rollback, FK/index, UTC and model-drift checks. InitialDomainSchema was not changed.

## Integration coverage

Tests use a separate randomly named PostgreSQL database per case, not an in-memory provider. Coverage includes atomic registration/profile creation, normalized duplicate rejection, safe account contracts, generic wrong/missing-account login, /me authentication, exact owner identity despite arbitrary request IDs, signed expiry/issuer/audience/tamper rejection, refresh replacement, replay family revocation, independent devices, concurrent rotation, logout idempotence/access revocation, logout-all, current-password checks, old-password rejection, hashed reset capture, reset single-use/expiry/session revocation, unavailable delivery without enumeration, host/DbContext lifecycle persistence, CSRF rejection, untrusted-origin CORS rejection, spoofed-forwarding-header-resistant throttling, production cookie flags and rejection of production capture.

Ownership tests create two complete graphs and query every one of the 15 owned entity types through OwnedData. Foreign IDs produce no rows; foreign edits/additions/deletions fail; shared/account types require separate policies; anonymous access fails. CODEX-002's relational ownership/constraints/concurrency/history tests continue to pass.

## Security review and corrections

- Framework hashing uses Identity V3 and 210,000 PBKDF2-SHA512 iterations. No password cryptography was implemented. No request-body, authorization, cookie or token logging was added.
- Session/reset secrets use RandomNumberGenerator and SHA-256 before persistence. Tests compare persisted digests with issued test secrets. Tokens have bounded expiry and explicit revocation/consumption.
- Account-row locks serialize security mutations. A concurrency test proves one refresh wins and replay revokes its family. The locking query explicitly selects PostgreSQL xmin to retain EF concurrency mapping.
- Every access request checks both account stamp and active family; logout/password-reset cannot leave a usable stale JWT until its nominal expiry.
- Production refresh cookies were strengthened from **Secure to **Host during review, preventing sibling-subdomain domain-cookie injection. Both cookies are Secure/HttpOnly/Strict at path /, without Domain. Only access is forwarded by the Next workspace check.
- Unsafe requests require trusted Origin plus a custom header. Credentialed CORS has no wildcard. Raw forwarded headers are ignored unless an exact trusted proxy is configured; production must sanitize ingress headers.
- Server layout validation and client gates protect the shell. Tokens never enter localStorage/sessionStorage. Refresh is single-flight with one retry and cross-tab Web Locks where supported. Reset fragments are removed from history; a StrictMode test verifies they survive effect replay in memory.
- Recovery is explicitly unavailable in production without a provider. Development capture is bounded, opt-in and inaccessible over HTTP; production rejects it. Email verification remains a truthful unverified state, not a fake delivery flow.
- Auth configuration validation was moved after host configuration to support the real integration host while still failing startup for missing secrets. Frontend CI now supplies a nonsecret API_INTERNAL_URL required by its build; no production secret was added.
- Framework/provider logging that can include sensitive exceptions/SQL is suppressed. Safe custom exception logging remains. API responses do not serialize User/session entities or accept a browser UserId as authority.
- Source searches checked unfinished markers, secrets/private keys, fake users, weak random generation, permissive CORS, browser token storage and plaintext token persistence. The dummy password-verification object is never persisted or authenticated; named test accounts exist only in test code.

No unresolved bypass was identified in this review. This is not a claim of an external audit. Documented operational limits include process-local throttling, strict replay behavior on lost responses, ASCII email normalization, no delivery provider/verification workflow, no automated token retention and no complete nonce-based script CSP. See [authentication](authentication.md).

## Executed verification commands

```sh
dotnet restore apps/api/CareerOS.sln --locked-mode
dotnet build apps/api/CareerOS.sln --configuration Release --no-restore
dotnet test apps/api/CareerOS.sln --configuration Release --no-build --logger "trx;LogFileName=codex-003.trx"
dotnet ef migrations add AuthenticationFoundation --project apps/api/src/CareerOS.Infrastructure --startup-project apps/api/src/CareerOS.Api --output-dir Persistence/Migrations --configuration Release
dotnet ef database update --project apps/api/src/CareerOS.Infrastructure --startup-project apps/api/src/CareerOS.Api --configuration Release
dotnet ef migrations has-pending-model-changes --project apps/api/src/CareerOS.Infrastructure --startup-project apps/api/src/CareerOS.Api --configuration Release --no-build
dotnet ef migrations script --idempotent --project apps/api/src/CareerOS.Infrastructure --startup-project apps/api/src/CareerOS.Api --configuration Release --no-build --output artifacts/AuthenticationFoundation.sql
dotnet format apps/api/CareerOS.sln --verify-no-changes --no-restore
npm run lint
npm run typecheck
npm test
npm run build
npm run format:check
docker compose --env-file .env.example config --quiet
git diff --check
git status --short --branch
```

Database tests ran with CAREEROS_TEST_POSTGRES pointing to a disposable loopback PostgreSQL cluster, using the official runtime already downloaded for CODEX-002. Test credentials were generated temporarily, never printed/committed, and the plaintext password file was removed after stopping the cluster. The additional empty browser-test database was dropped. Ignored runtime/data artifacts remain local. CI retains RUN_CONTAINER_TESTS=true and must independently run the same suite with PostgreSQL 17 Testcontainers.

## Browser limitation

Automatic approval review rejected starting the frontend verification server on port 3001, reporting only “blocked by policy.” No alternative launch was attempted. The already-running localhost:3000 process still served CODEX-001's old build and returned 404 for /login; it cannot validate the new UI. Its tab was restored. No live visual, responsive-browser or end-to-end browser result is claimed. Accessible forms, account state, route guards, refresh and error behavior are covered by the executed frontend tests and production build.

The task's final report records the one commit, push and GitHub CI results after this pre-commit record.
