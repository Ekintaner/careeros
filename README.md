# CareerOS

CareerOS is a full-stack job search operating system for managing applications, resumes, interviews, skills, analytics and career intelligence.

## Overview

A focused workspace for making a job search easier to understand and manage. This repository contains **CODEX-001: Project Foundation & Architecture** only.

## Product Vision

Connect opportunities, application progress, career documents and actionable insights without losing the context behind each career decision.

## Current Status

**Implemented:** responsive application shell, honest empty Overview, disabled future navigation, typed API client, ASP.NET API host, structured request logging, error handling, health checks, EF Core/PostgreSQL and Redis connectivity, development infrastructure, tests and CI.

**Planned:** authentication, profiles, companies, jobs, applications, Kanban, resumes, parsing, explainable matching, skill gaps, interviews, tasks, documents, notifications, analytics and AI assistance. No product records, authentication or database tables exist yet. The shell is a visual preview, not an authenticated session.

## Technology Stack

- Node.js 24, npm workspaces, Next.js 16.3.5 App Router, React 19.3, TypeScript, Tailwind CSS 4.
- ASP.NET Core / .NET 8, EF Core 8, Npgsql, StackExchange.Redis, Swashbuckle OpenAPI.
- PostgreSQL 17 and Redis 7.4 through Docker Compose.
- Vitest, Testing Library, xUnit, WebApplicationFactory and Testcontainers.

.NET 8 matches the SDK installed for this phase. It reaches end of support on November 10, 2026; upgrade to .NET 10 LTS before production deployment. See the [Microsoft support policy](https://dotnet.microsoft.com/en-us/platform/support/policy).

## Architecture

One monorepo, two independently runnable applications. The backend uses pragmatic Clean Architecture: Domain ← Application ← Infrastructure, composed by Api. No repository wrapper, mediator, product scaffolding or shared JavaScript package is added without a concrete need. See [architecture](docs/architecture.md).

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

The root `.env` is consumed by **Docker Compose only**. ASP.NET reads `ConnectionStrings__PostgreSql`, `ConnectionStrings__Redis`, and normal .NET configuration sources. For local credentials prefer `dotnet user-secrets`. Next.js reads `apps/web/.env.local`; `NEXT_PUBLIC_API_BASE_URL` is public, build-time browser configuration, never a place for secrets. The overview makes no API calls yet. Missing API URL fails clearly when the client is first used.

The API fails startup when required connection strings are absent or malformed. Unreachable configured dependencies do not prevent liveness; readiness reports failure. Development CORS allows `http://localhost:3000` only. Override `Cors__AllowedOrigins__0` if needed. Configure `AllowedHosts` and an explicit production HTTPS/reverse-proxy policy before deployment; there is no production deployment configuration in this phase.

## Docker

`docker compose up -d --wait` starts local PostgreSQL and Redis with health checks and persistent named volumes. Ports bind only to loopback. `docker compose down` retains data. See [infrastructure notes](infrastructure/README.md). Never reuse local development credentials in production.

## Frontend

`npm run dev`, `npm run lint`, `npm run typecheck`, `npm test`, `npm run build`. To serve a production build locally: `npm run start --workspace apps/web`.

The shell uses semantic navigation, focus indicators, a skip link and a native mobile disclosure menu. Future navigation is disabled and does not simulate working features. Feature folders and libraries such as TanStack Query, React Hook Form and Zod will be introduced with their first real consumer.

## Backend

`dotnet run --project apps/api/src/CareerOS.Api`. Development OpenAPI JSON is at `/swagger/v1/swagger.json` and Swagger UI at `/swagger`. `/health/live` checks the running host; `/health` actually probes PostgreSQL and Redis and returns 503 if unavailable. Neither endpoint exposes connection strings or exception details. `X-Correlation-ID` is validated and returned with responses, and appears in structured JSON logs and ProblemDetails.

Future HTTP product endpoints use `/api/v1/...`; health endpoints remain unversioned. No empty product endpoints or migrations are manufactured. See the migration workflow in [development](docs/development.md).

## Testing

```sh
npm test
dotnet test apps/api/CareerOS.sln
```

The default backend suite tests real HTTP pipeline behavior in-process and intentionally unavailable dependencies. Set `RUN_CONTAINER_TESTS=true` with Docker running to enable the separate real PostgreSQL Testcontainers test; otherwise it is visibly skipped. CI enables it and fails if containers cannot run.

## CI

Pushes to `main` and pull requests run frontend installation, formatting, lint, type checks, tests and production build; backend locked restore, Release build and tests; and Compose validation. CI has read-only repository permissions and no deployment step.

## Roadmap

1. CODEX-001 — foundation (this phase).
2. Authentication and ownership design, with a supported-runtime upgrade before production.
3. First real vertical slice: companies, opportunities and application tracking.
4. Documents, resumes, interviews and follow-ups.
5. Explainable matching, analytics and career intelligence.

## License

[MIT](LICENSE).
