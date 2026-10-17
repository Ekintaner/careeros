# Development guide

## Tooling and initial setup

Use Node 24, npm 11, .NET SDK 8.0.425 and Docker Compose v2. Run commands from the repository root unless stated otherwise.

```sh
npm ci
dotnet restore apps/api/CareerOS.sln --locked-mode
dotnet tool restore
```

Copy `.env.example` to `.env` yourself and replace its password placeholder. Keep this local secrets file ignored by Git. On PowerShell use `Copy-Item .env.example .env`; on POSIX use `cp .env.example .env`. Start Docker Desktop/Engine, then:

```sh
docker compose up -d --wait
docker compose ps
```

## API configuration

Configure local secrets using a password you chose for Compose (replace `<local-password>`):

```sh
dotnet user-secrets set "ConnectionStrings:PostgreSql" "Host=localhost;Port=5432;Database=careeros;Username=careeros;Password=<local-password>" --project apps/api/src/CareerOS.Api
dotnet user-secrets set "ConnectionStrings:Redis" "127.0.0.1:6379" --project apps/api/src/CareerOS.Api
dotnet run --project apps/api/src/CareerOS.Api
```

User-secrets are development-only local storage, not an encrypted production vault. The launch profile enables Development and port 5080. The root `.env` is not read by ASP.NET. Alternatively set process variables:

```powershell
$env:ConnectionStrings__PostgreSql = 'Host=localhost;Port=5432;Database=careeros;Username=careeros;Password=<local-password>'
$env:ConnectionStrings__Redis = 'localhost:6379'
```

```sh
export ConnectionStrings__PostgreSql='Host=localhost;Port=5432;Database=careeros;Username=careeros;Password=<local-password>'
export ConnectionStrings__Redis='localhost:6379'
```

Open `http://localhost:5080/swagger` in Development. `/health/live` should return 200 even if dependencies are down. `/health` requires both dependencies and returns 503 otherwise. Health endpoints are deliberately unversioned. Authentication routes are under /api/v1/auth; see [authentication](authentication.md).

## Frontend workflow

Copy `apps/web/.env.example` to `apps/web/.env.local` and adjust the API origin if necessary.

```sh
npm run dev
npm run lint
npm run typecheck
npm test
npm run build
npm run start --workspace apps/web
```

Next.js serves port 3000. The last command requires a completed build; do not run dev and production on the same port simultaneously. Public environment variables are embedded at build time. Account pages render independently; workspace access requires the configured API and a real login. Service failures show safe errors, not a fake session.

## Backend workflow and migrations

```sh
dotnet build apps/api/CareerOS.sln --configuration Release --no-restore
dotnet test apps/api/CareerOS.sln --configuration Release --no-build
```

EF tools are local and versioned in `.config/dotnet-tools.json`. CODEX-002 includes the generated `InitialDomainSchema` migration. With the database configured and running, apply it explicitly:

```sh
dotnet ef database update --project apps/api/src/CareerOS.Infrastructure --startup-project apps/api/src/CareerOS.Api
dotnet ef migrations has-pending-model-changes --project apps/api/src/CareerOS.Infrastructure --startup-project apps/api/src/CareerOS.Api
```

For these commands, explicitly set `ASPNETCORE_ENVIRONMENT=Development` to use user-secrets, or supply connection strings through environment variables. No database is created or migrated during host startup. Do not use `EnsureCreated` alongside migrations. For future schema changes use `dotnet ef migrations add <DescriptiveName> --project apps/api/src/CareerOS.Infrastructure --startup-project apps/api/src/CareerOS.Api --output-dir Persistence/Migrations`, then review the generated migration and snapshot. Generate a deployment script with `dotnet ef migrations script --idempotent` using the same project flags. Never edit an already deployed migration in place.

The real database tests cover rollback to migration 0 and reapplication in disposable databases. Rolling this schema back deletes its data; do not run rollback against a working user database as routine troubleshooting. See [database architecture](database.md) for schema, constraints and retention decisions.

## Testing

Frontend tests use Vitest/jsdom and Testing Library. Backend unit tests validate request identifiers and important domain invariants; in-process integration tests verify actual middleware and honest unavailable-dependency statuses without requiring Docker.

For real PostgreSQL integration coverage, enable Docker explicitly:

```powershell
$env:RUN_CONTAINER_TESTS = 'true'
dotnet test apps/api/CareerOS.sln
```

```sh
RUN_CONTAINER_TESTS=true dotnet test apps/api/CareerOS.sln
```

Testcontainers creates and disposes its own database container with dynamic credentials/ports. Each test creates and removes a separate randomly named database inside that server. It does not use the development volume. A stopped engine is a failure when opted in; otherwise the PostgreSQL tests are explicitly skipped. CI opts in on Linux runners and runs the complete schema suite.

If Docker is unavailable, set `CAREEROS_TEST_POSTGRES` to the connection string for a separately provisioned **disposable PostgreSQL 17 server**, with a role permitted to create databases. This enables the same tests without changing them; the fixture creates/drops only its own `careeros_test_<uuid>` databases. Do not use production credentials or a production server. Keep this connection string in the process environment, never source control. CI continues to use Testcontainers by default.

## Docker workflow

```sh
docker compose --env-file .env.example config --quiet
docker compose up -d --wait
docker compose logs --tail=100
docker compose down
```

Use the example only for validation. Do not start services with its password placeholder. `down` retains volumes; adding `--volumes` destroys local data. Database initialization variables apply only when the PostgreSQL volume is first initialized; changing `.env` does not change an existing database password.

## Formatting and dependency changes

```sh
npm run format
npm run format:check
dotnet format apps/api/CareerOS.sln --verify-no-changes --no-restore
```

Commit npm and NuGet lockfiles. To intentionally update NuGet resolution, restore without `--locked-mode`, review changes, then verify locked restore. Keep EF Core, its design tool and the PostgreSQL provider on compatible majors.

## Troubleshooting

- **SDK not found:** install the SDK selected by `global.json`; do not silently retarget the project.
- **Connection configuration validation fails:** set both API connection strings. A Compose `.env` alone is insufficient.
- **Readiness 503:** inspect service health, ports and local credentials; a healthy liveness check does not imply healthy dependencies.
- **Docker pipe/socket missing:** start Docker Desktop/Engine. Compose configuration validation works without the engine, container tests do not.
- **CORS failure:** update `Cors:AllowedOrigins` to the exact frontend origin, without a trailing slash. Production requires explicit HTTPS origins. Mutations also require the trusted Origin and X-CareerOS-CSRF header.
- **Port conflict:** adjust Compose ports and corresponding API connection strings together; changing only one breaks readiness.
- **API URL missing:** set `apps/web/.env.local` and restart/rebuild Next.js.
- **Restore blocked by network:** allow registry.npmjs.org and api.nuget.org, then retry; do not suppress restore errors.

Product CRUD, documents, matching, planning, analytics and optional local assistance are implemented. Public hosting and production recovery delivery remain operator work; see the final README and release operations.

## Profile/settings workflow (CODEX-004)

Apply the full migration chain using the existing EF update command, then register or sign in normally. Open `/profile` for career information, preferences and skill management; open `/settings` for account information, display-name editing and password change. There is no seed/default user. Skills use only the local PostgreSQL vocabulary. All profile fields start empty. Email is read-only; verification delivery remains unavailable.

Edits include a server row version. To check conflicts, open the profile in two tabs, save one, then save the stale second tab: expect a 409 and preserved draft with a reload option. Changing a password signs out every session, including the current browser. Reauthenticate with the new password.

No new npm/NuGet dependency, paid API, external skill service, cloud provisioning or configuration secret is required. Single-user core functionality must remain usable without a mandatory paid or usage-metered service. Keep future paid integrations optional and disabled by default. See [profile architecture](profile.md) and [CODEX-004 verification](verification-codex004.md).

## Authentication setup (CODEX-003)

Alongside database/Redis connection strings, set the following through user-secrets or the process environment. Choose a cryptographically random signing key (base64-encoded, at least 32 random bytes) in your own secret store; no example secret is usable.

```sh
dotnet user-secrets set "Auth:Issuer" "careeros-local" --project apps/api/src/CareerOS.Api
dotnet user-secrets set "Auth:Audience" "careeros-web" --project apps/api/src/CareerOS.Api
dotnet user-secrets set "Auth:SigningKey" "<base64-random-key>" --project apps/api/src/CareerOS.Api
```

Default lifetimes are five minutes for access and 30 days absolute for refresh. Set Auth**AccessTokenMinutes/Auth**RefreshTokenDays to override within validated limits. Missing critical authentication settings fail startup, including Development; no fallback signing key or user is invented. Apply all five migrations explicitly before using the workspace.

Set `API_INTERNAL_URL=http://localhost:5080` in apps/web/.env.local. This is server-only configuration; restart/rebuild Next after changing its rewrite destination. The browser uses same-origin `/api/v1`, not a public backend URL. Keep `Cors:AllowedOrigins` equal to the actual web origin (default http://localhost:3000). No cookies or credentials belong in NEXT_PUBLIC variables.

Register through /register, then Overview shows the account. Logout revokes its session. Forgot-password deliberately returns 503 until delivery is configured. For isolated development/debugging only, opt in with `Auth__DevelopmentCapture=true`: inspect DevelopmentResetCapture through a debugger or the integration-test DI container and consume its Take(email) value. Nothing is printed and no capture endpoint exists. Use `/reset-password#token=<captured-token>` locally. Capture is bounded, expires and is forbidden outside Development. Production email and verification are not implemented.

Production requires Secure cookies/HTTPS origins and a real secret manager; configure AllowedHosts and a trusted ingress before exposure. Do not trust arbitrary forwarded headers. See the proxy, throttling, delivery and retention requirements in [authentication](authentication.md).

The existing PostgreSQL test switch now also runs authentication and complete two-user ownership tests. Test signing keys/passwords are generated or test-only; no default authentication bypass exists in the application. The generated auth migration can be inspected with the same EF commands above.

## Companies/jobs workflow (CODEX-005)

Apply CompanyAndJobLifecycle with the existing EF update command. Start Compose, API and web using the commands above. Register or sign in, open /companies and add a company, then open /jobs to save an opportunity using that company. Search/filter controls submit server queries. Job details provide edit, archive/restore and restricted delete. A company with jobs cannot be deleted; archive a job when history must be retained. No seed data or external job provider is required.

Keep the API and web terminal sessions running while using the preview at http://localhost:3000. API runs at http://localhost:5080; /health checks PostgreSQL and Redis. Stopping those sessions makes preview unavailable without indicating a code defect. After restarting, use the actual frontend port and ensure its origin is in the API CORS configuration.

## Applications workflow (CODEX-006)

Apply the migration chain with the existing EF update command. Open /applications after signing in. Track an existing job as Saved or Applied; stage controls persist a transition and history atomically. Job detail links to the existing application. Notes are plain text; resume choices include owned metadata only. Archive preserves history and Restore returns to the recorded previous status. No upload, seed user, new secret or paid dependency is required. See [applications and pipeline](applications-and-pipeline.md) for endpoint/query contracts and limitations.

## Documents/resumes workflow (CODEX-007)

No additional migration or dependency is required. The default private file root is LocalApplicationData/CareerOS/documents, outside the repository. Optionally configure an absolute `Documents__StorageRoot` outside Git and the application directory. Restrict OS permissions to the API account. Start the usual local stack, sign in, then use /documents or /resumes. PDF files are limited to 5 MiB. See [storage, deletion and paired backup/restore](documents-and-resumes.md).

## Interviews and tasks (CODEX-010)

Start the usual PostgreSQL/Redis/API/web stack; no new migration or configuration is required. Open /interviews or /tasks after signing in. Schedule against an existing application, keep private interview notes, and create standalone or application-linked follow-ups. Use the overdue and upcoming views to plan the next step. The editor names the device timezone and offers UTC entry. See [contracts and limitations](interviews-and-tasks.md).

## Analytics (CODEX-011)

Use /analytics after signing in. Calendar filters use IANA zones (for example Europe/Kyiv); UTC is the default. See [metric definitions](analytics-and-reporting.md) and [verification](verification-codex011.md). Existing PostgreSQL test switches include analytics integration tests. No additional migration or package is needed.

## Optional local AI (CODEX-012)

Leave LocalAi:Enabled false to use every core feature without an inference server. Optional manual Ollama setup, cloud-disabled preflight requirements, hardware caveats and protocol-test boundaries are documented in [local AI assistance](local-ai-assistance.md). No model installation or download is part of startup/build/tests. Existing PostgreSQL test switches discover the new assistance ownership/security tests.

## Product UI verification (CODEX-013)

The Overview now reads /api/v1/dashboard. Existing test switches include dashboard ownership and time-boundary checks. No additional setup or migration is required. See [verification and limitations](verification-codex013.md) and [shared presentation conventions](design-system.md).
