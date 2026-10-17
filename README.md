# CareerOS

CareerOS is a full-stack job search operating system for managing applications, resumes, interviews, skills, analytics and career intelligence.

## Overview

A focused workspace for making a job search easier to understand and manage. This repository contains **CODEX-001: Project Foundation & Architecture**, **CODEX-002: Core Domain & PostgreSQL Data Architecture**, **CODEX-003: Authentication, Security & Data Ownership**, **CODEX-004: Career Profile, Skills & Account Settings**, and **CODEX-005: Companies & Job Opportunities**.

## Product Vision

Connect opportunities, application progress, career documents and actionable insights without losing the context behind each career decision.

## Current Status

**Implemented:** responsive application shell, honest empty Overview, disabled future navigation, typed API client, ASP.NET API host, structured request logging, error handling, health checks, EF Core/PostgreSQL and Redis connectivity, 17 domain/persistence entities, an initial PostgreSQL schema migration, ownership constraints, auditable timestamps, optimistic concurrency, development infrastructure, tests and CI.

**Implemented in CODEX-003:** registration/login/recovery pages, protected workspace, JWT access cookies, rotating refresh sessions, replay detection, logout/all, password change/reset, current-user ownership boundary, rate limits and CSRF protection. See [authentication](docs/authentication.md).

**Implemented in CODEX-004:** authenticated `/profile` and `/settings`, persistent career information/job preferences, canonical skill search and owned skill management, display-name editing and password-change UI. See [profile architecture](docs/profile.md).

**Implemented in CODEX-005:** owner-scoped company and job CRUD, bounded search/filter/sort lists, job detail, archive/restore and concurrency-safe restrictive deletion. See [companies and jobs](docs/companies-and-jobs.md).

**Implemented in CODEX-007:** authenticated owner-scoped PDF documents and resumes, local private storage, downloads, restricted deletion, primary resume selection and application assignment. See [documents and resumes](docs/documents-and-resumes.md).

**Planned:** interviews, tasks and notifications; parsing, explainable matching, skill gaps, analytics and optional AI assistance. These future tools remain disabled. Production email delivery is not configured and recovery fails safely until an adapter is installed. No records are seeded automatically.

## Zero-cost requirement

CareerOS must remain fully usable by a single personal user without requiring a paid API, paid AI provider, paid database, paid Redis provider, paid storage provider, paid email provider, paid domain, or mandatory paid hosting service. Prefer open-source, local/self-hostable, or genuinely usable free solutions. Any paid integration must be optional, disabled by default, and must not be necessary for core functionality. Mandatory usage-metered SaaS is excluded. CODEX-004/005 add no dependency or cloud service.

## Technology Stack

- Node.js 24, npm workspaces, Next.js 16.3.5 App Router, React 19.3, TypeScript, Tailwind CSS 4.
- ASP.NET Core / .NET 8, EF Core 8, Npgsql, StackExchange.Redis, Swashbuckle OpenAPI.
- PostgreSQL 17 and Redis 7.4 through Docker Compose.
- Vitest, Testing Library, xUnit, WebApplicationFactory and Testcontainers.

.NET 8 matches the SDK installed for this phase. It reaches end of support on November 10, 2026; upgrade to .NET 10 LTS before production deployment. See the [Microsoft support policy](https://dotnet.microsoft.com/en-us/platform/support/policy).

## Architecture

One monorepo, two independently runnable applications. The backend uses pragmatic Clean Architecture: Domain ← Application ← Infrastructure, composed by Api. No repository wrapper, mediator or shared JavaScript package is added without a concrete need. See [architecture](docs/architecture.md) and the [database model](docs/database.md).

## Repository Structure

```text
apps/
  web/src/
    app/                    App Router pages and root layout
    components/layout/      Responsive shell and navigation
    components/ui/          Shared icon primitive
    lib/                    Typed HTTP client
    styles/                 Design tokens and responsive styling
    test/                   Test setup
  api/
    CareerOS.sln
    src/
      CareerOS.Domain/
      CareerOS.Application/
      CareerOS.Infrastructure/
      CareerOS.Api/
    tests/
      CareerOS.Tests.Unit/
      CareerOS.Tests.Integration/
docs/                       Architecture, setup and verification notes
infrastructure/             Development infrastructure guidance
scripts/                    Cross-platform developer command guide
.github/workflows/          CI
.config/                    Local dotnet-ef tool manifest
docker-compose.yml
```

## Prerequisites

Node.js 24 (see `.nvmrc`), npm 11, .NET SDK 8.0.425 (see `global.json`), and Docker Engine with Compose v2 for infrastructure and container tests. Docker is not required for frontend development or ordinary backend tests.

## Local Development

See [development guide](docs/development.md) for complete PowerShell and POSIX commands.

1. `npm ci`
2. Copy `.env.example` to a local `.env`, choose a local database password, then `docker compose up -d --wait`.
3. Configure API connection strings using environment variables or .NET user-secrets as documented below.
4. `dotnet restore apps/api/CareerOS.sln --locked-mode`
5. `dotnet run --project apps/api/src/CareerOS.Api` (API: `http://localhost:5080`).
6. Copy `apps/web/.env.example` to `apps/web/.env.local`, then `npm run dev` (web: `http://localhost:3000`).

## Environment Configuration

The root `.env` is consumed by **Docker Compose only**. ASP.NET reads `ConnectionStrings__PostgreSql`, `ConnectionStrings__Redis`, and normal .NET configuration sources. For local credentials prefer `dotnet user-secrets`. Next.js reads server-only `API_INTERNAL_URL` from `apps/web/.env.local` for its same-origin API rewrite and server account checks. No backend secret belongs in a NEXT_PUBLIC variable. Authentication additionally requires issuer, audience, a configured signing key and explicit frontend origins; see [setup](docs/development.md).

The API fails startup when required connection strings are absent or malformed. Unreachable configured dependencies do not prevent liveness; readiness reports failure. Credentialed CORS/CSRF allow explicitly configured origins; Development defaults to `http://localhost:3000`. Override `Cors__AllowedOrigins__0` if needed. Configure `AllowedHosts` and an explicit production HTTPS/reverse-proxy policy before deployment; there is no production deployment configuration in this phase.

## Docker

`docker compose up -d --wait` starts local PostgreSQL and Redis with health checks and persistent named volumes. Ports bind only to loopback. `docker compose down` retains data. See [infrastructure notes](infrastructure/README.md). Never reuse local development credentials in production.

## Frontend

`npm run dev`, `npm run lint`, `npm run typecheck`, `npm test`, `npm run build`. To serve a production build locally: `npm run start --workspace apps/web`.

The shell uses semantic navigation, focus indicators, a skip link and a native mobile disclosure menu. Future navigation is disabled and does not simulate working features. Feature folders and libraries such as TanStack Query, React Hook Form and Zod will be introduced with their first real consumer.

## Backend

`dotnet run --project apps/api/src/CareerOS.Api`. Development OpenAPI JSON is at `/swagger/v1/swagger.json` and Swagger UI at `/swagger`. `/health/live` checks the running host; `/health` actually probes PostgreSQL and Redis and returns 503 if unavailable. Neither endpoint exposes connection strings or exception details. `X-Correlation-ID` is validated and returned with responses, and appears in structured JSON logs and ProblemDetails.

HTTP product endpoints use `/api/v1/...`; health endpoints remain unversioned. The generated `InitialDomainSchema`, `AuthenticationFoundation`, `CareerProfilePreferences`, and `CompanyAndJobLifecycle` migrations define the schema. Apply them explicitly; API startup does not migrate or seed databases. See the migration workflow in [development](docs/development.md).

## Testing

```sh
npm test
dotnet test apps/api/CareerOS.sln
```

The default backend suite tests domain invariants, real HTTP pipeline behavior in-process and intentionally unavailable dependencies. Set `RUN_CONTAINER_TESTS=true` with Docker running to enable real PostgreSQL migration/constraint/concurrency tests; otherwise they are visibly skipped. CI enables them and fails if containers cannot run. A disposable PostgreSQL server can also be supplied through `CAREEROS_TEST_POSTGRES`; see [development](docs/development.md).

## CI

Pushes to `main` and pull requests run frontend installation, formatting, lint, type checks, tests and production build; backend locked restore, Release build and tests; and Compose validation. CI has read-only repository permissions and no deployment step.

## Roadmap

1. CODEX-001 — foundation (complete).
2. CODEX-002 — core domain and PostgreSQL schema (complete).
3. CODEX-003 — authentication and server-enforced ownership (complete; production delivery/deployment remain separate).
4. CODEX-004 — career profile, skills and account settings (complete).
5. CODEX-005 — companies and opportunities (complete).
6. CODEX-006 — application tracking, pipeline and status history (complete).
7. CODEX-007 — resume and document management (complete).
8. Interviews and follow-ups.
9. Explainable matching, analytics and career intelligence.

## License

[MIT](LICENSE).

## Application tracking (CODEX-006)

Track owned jobs as applications, manage a responsive pipeline, record status history and notes, attach existing resume metadata, and archive/restore without losing history. See [applications and pipeline](docs/applications-and-pipeline.md) and [CODEX-006 verification](docs/verification-codex006.md). No new dependency or paid service is required.
