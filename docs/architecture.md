# Architecture

## Principles

Keep dependencies explicit, compile with strict types and nullable references, and add abstractions only for real use cases. Separate business rules from delivery and persistence. Prefer a modular monolith over distributed services while requirements are still forming. Never make the UI or health checks claim data or infrastructure exists when it does not.

## Monorepo

`apps/web` and `apps/api` have independent runtimes. npm workspaces provide one JavaScript lockfile without an extra orchestration platform. The .NET solution uses per-project NuGet lockfiles. Root configuration captures formatting, runtime choices and local infrastructure.

## Backend boundaries and dependencies

| Project        | Responsibility                                          | Allowed project dependencies |
| -------------- | ------------------------------------------------------- | ---------------------------- |
| Domain         | Business concepts, exceptions and rules                 | None                         |
| Application    | Future use cases, DTOs, validation and interfaces       | Domain                       |
| Infrastructure | EF Core, PostgreSQL, Redis and future external adapters | Application, Domain          |
| Api            | HTTP, configuration, middleware and composition         | Application, Infrastructure  |

Domain now contains 17 entities with protected mutation methods, explicit skill associations, ordered application history and validation. Application intentionally contains no executable use case yet. Infrastructure owns EF configurations, the generated InitialDomainSchema migration, UTC audit handling, PostgreSQL concurrency mapping and Redis connection lifetime. API composes services but exposes no product endpoints. Tests reference the projects whose types they test. No generic repositories, mediator pipeline or automatic database migrations at startup. See [database architecture](database.md) for the complete schema and ownership decisions.

Minimal endpoints are sufficient for operational health. Future product endpoints should be grouped into cohesive endpoint modules with `/api/v1` route groups, typed contracts and explicit validation. Breaking contracts introduce a new URL major version; additive changes remain in v1. No versioning library is required before the first product API.

## HTTP and observability

Exception and status-code middleware emit RFC 7807-compatible ProblemDetails; unknown exceptions produce a generic 500 response. Future domain exceptions require explicit, reviewed mappings rather than treating every exception as a client error. Request IDs accept only 1–64 ASCII letters, digits, dots, underscores or hyphens; other values are replaced. JSON request logs carry the same identifier, method, path, status and elapsed time. Bodies, authorization headers and query strings are not logged by custom middleware. Configure log retention and redaction before ingesting real personal data; framework and provider logs also require review.

OpenAPI and Swagger UI are development-only. CORS is development-only, explicit-origin, without credentials. Production transport security, hostnames, proxy trust, authentication, authorization, rate limits and protected operational diagnostics must be established in subsequent deployment/authentication work. This foundation is not safe to expose as a complete user application.

## Frontend architecture

App Router owns routing and page composition. The shell is a server component with native HTML disclosure behavior on mobile; no global state provider is needed. Shared colors, spacing, type scale, radii and layout dimensions live in `styles/globals.css`. The custom SVG icon primitive avoids a dependency for eleven simple outline icons.

`lib/api-client.ts` centralizes the origin, JSON success parsing, empty responses, HTTP ProblemDetails and network errors. Callers provide the expected TypeScript contract; runtime response validation will belong beside real feature schemas. Cancellation is passed through. The client prevents URL paths from changing the configured origin and never invents API responses. No calls are made until a real feature needs them.

Add `features/<feature>` for cohesive behavior once implemented. Add shared hooks, types, providers, accessible components, query state and form validation only when there is an actual consumer. Route-level loading/error UI should correspond to real data operations, not fake delays or simulated failures.

## Infrastructure and configuration

Compose provides PostgreSQL and Redis only; application processes run on the host. Loopback bindings support local development without exposing database ports to the LAN. Named volumes preserve state. Redis has append-only persistence and no local authentication because it is loopback-only; production Redis requires private networking, authentication and transport protection.

ASP.NET options validate connection-string structure at startup. Connectivity is checked at readiness time with bounded timeouts. `/health/live` does not test dependencies. `/health` performs a PostgreSQL query and Redis ping and reports individual statuses without details. The Redis multiplexer is created lazily and shared; it can reconnect when Redis becomes available. EF connection resiliency is configured, but no application transaction policy is invented yet.

Root `.env` serves Compose; ASP.NET environment/user-secrets and web `.env.local` are separate. Secrets never belong in source or `NEXT_PUBLIC_*` settings. Packages are pinned through lockfiles; database image tags follow compatible major lines, so exact image digests should be pinned for future deployment reproducibility.

## Testing philosophy

Test meaningful contracts: accessible shell structure and unavailable navigation; client success/error/network behavior; domain invariants; request ID validation; HTTP errors, CORS, liveness and unavailable dependency readiness. PostgreSQL integration tests exercise migration apply/rollback/reapply, real constraints, owner-consistent relationships, atomic history, UTC audit handling and xmin concurrency. CI explicitly enables Testcontainers; local testing can use a separately provisioned disposable PostgreSQL server. Each database test owns its own randomly named database. Test-only throwing middleware exists only in the integration assembly and never ships in the API.

## Future extension

CODEX-002 establishes account IDs and cross-owner relational integrity, but it does not authorize reads/writes. CODEX-003 must implement trusted authentication, scoped server queries and command authorization before exposing personal data. Retention/erasure policy and shared-skill administration also remain explicit future work. Then implement a small vertical slice using the existing domain/schema, API contracts and feature UI. Upgrade to .NET 10 LTS before .NET 8 support ends on November 10, 2026. Match-result persistence waits for a defined algorithm, input versioning and explanation contract; no arbitrary score columns are introduced now.
