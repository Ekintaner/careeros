# Development guide

## Tooling and initial setup

Use Node 24, npm 11, .NET SDK 8.0.425 and Docker Compose v2. Run commands from the repository root unless stated otherwise.

```sh
npm ci
dotnet restore apps/api/CareerOS.sln --locked-mode
dotnet tool restore
```

Copy `.env.example` to `.env` yourself and replace its password placeholder. The agent does not create a secrets file. On PowerShell use `Copy-Item .env.example .env`; on POSIX use `cp .env.example .env`. Start Docker Desktop/Engine, then:

```sh
docker compose up -d --wait
docker compose ps
```

## API configuration

Configure local secrets using a password you chose for Compose (replace `<local-password>`):

```sh
dotnet user-secrets set "ConnectionStrings:PostgreSql" "Host=localhost;Port=5432;Database=careeros;Username=careeros;Password=<local-password>" --project apps/api/src/CareerOS.Api
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379" --project apps/api/src/CareerOS.Api
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

Open `http://localhost:5080/swagger` in Development. `/health/live` should return 200 even if dependencies are down. `/health` requires both dependencies and returns 503 otherwise. Health endpoints are deliberately unversioned. The v1 product contract is reserved but has no endpoints yet.

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

Next.js serves port 3000. The last command requires a completed build; do not run dev and production on the same port simultaneously. Public environment variables are embedded at build time. The shell renders without API availability and never fabricates user statistics.

## Backend workflow and migrations

```sh
dotnet build apps/api/CareerOS.sln --configuration Release --no-restore
dotnet test apps/api/CareerOS.sln --configuration Release --no-build
```

EF tools are local and versioned in `.config/dotnet-tools.json`. After introducing the first real domain mapping, create and inspect a migration:

```sh
dotnet ef migrations add InitialDomain --project apps/api/src/CareerOS.Infrastructure --startup-project apps/api/src/CareerOS.Api --output-dir Persistence/Migrations
dotnet ef database update --project apps/api/src/CareerOS.Infrastructure --startup-project apps/api/src/CareerOS.Api
```

For these commands, explicitly set `ASPNETCORE_ENVIRONMENT=Development` to use user-secrets, or supply connection strings through environment variables. No database is created or migrated during host startup. Do not use `EnsureCreated` alongside migrations. No empty baseline migration is included because the model has no tables.

## Testing

Frontend tests use Vitest/jsdom and Testing Library. Backend unit tests validate request identifiers; in-process integration tests verify actual middleware and honest unavailable-dependency statuses without requiring Docker.

For real PostgreSQL integration coverage, enable Docker explicitly:

```powershell
$env:RUN_CONTAINER_TESTS = 'true'
dotnet test apps/api/CareerOS.sln
```

```sh
RUN_CONTAINER_TESTS=true dotnet test apps/api/CareerOS.sln
```

Testcontainers creates and disposes its own database container with dynamic credentials/ports. It does not use the development volume. A stopped engine is a failure when opted in; otherwise the test is explicitly skipped. CI opts in on Linux runners.

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
- **CORS failure:** update `Cors:AllowedOrigins` to the exact frontend origin, without a trailing slash. Production has no enabled CORS policy yet.
- **Port conflict:** adjust Compose ports and corresponding API connection strings together; changing only one breaks readiness.
- **API URL missing:** set `apps/web/.env.local` and restart/rebuild Next.js.
- **Restore blocked by network:** allow registry.npmjs.org and api.nuget.org, then retry; do not suppress restore errors.

No production deployment, authentication, personal-data persistence or product workflows are included in this phase.
