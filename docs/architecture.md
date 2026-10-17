# Architecture

## Principles

Keep dependencies explicit, compile with strict types and nullable references, and add abstractions only for real use cases. Separate business rules from delivery and persistence. Prefer a modular monolith over distributed services while requirements are still forming. Never make the UI or health checks claim data or infrastructure exists when it does not.

Core functionality must remain usable by one personal user with no paid API, AI, database, Redis, storage, email, domain, hosting or usage-metered SaaS requirement. Prefer local/self-hostable and genuinely usable free options; paid integrations must be optional and disabled by default. See the permanent rule in [profile architecture](profile.md).

## Monorepo

`apps/web` and `apps/api` have independent runtimes. npm workspaces provide one JavaScript lockfile without an extra orchestration platform. The .NET solution uses per-project NuGet lockfiles. Root configuration captures formatting, runtime choices and local infrastructure.

## Backend boundaries and dependencies

| Project        | Responsibility                                          | Allowed project dependencies |
| -------------- | ------------------------------------------------------- | ---------------------------- |
| Domain         | Business concepts, exceptions and rules                 | None                         |
| Application    | Future use cases, DTOs, validation and interfaces       | Domain                       |
| Infrastructure | EF Core, PostgreSQL, Redis and future external adapters | Application, Domain          |
| Api            | HTTP, configuration, middleware and composition         | Application, Infrastructure  |

Domain contains the 17 original entities with protected mutation methods, explicit skill associations, ordered application history and validation, plus refresh/reset token lifecycle entities. Application defines authentication contracts, password policy, current-user and recovery-delivery boundaries. Infrastructure owns EF configurations, the generated InitialDomainSchema migration, UTC audit handling, PostgreSQL concurrency mapping and Redis connection lifetime. API composes services and exposes the authentication endpoint module; profile/skills/account endpoints are implemented in CODEX-004; other product CRUD remains deferred. Tests reference the projects whose types they test. No generic repositories, mediator pipeline or automatic database migrations at startup. See [database architecture](database.md) for the complete schema and ownership decisions.

Minimal endpoints are sufficient for operational health. Future product endpoints should be grouped into cohesive endpoint modules with `/api/v1` route groups, typed contracts and explicit validation. Breaking contracts introduce a new URL major version; additive changes remain in v1. No versioning library is required before the first product API.

## HTTP and observability

Exception and status-code middleware emit RFC 7807-compatible ProblemDetails; unknown exceptions produce a generic 500 response. Future domain exceptions require explicit, reviewed mappings rather than treating every exception as a client error. Request IDs accept only 1–64 ASCII letters, digits, dots, underscores or hyphens; other values are replaced. JSON request logs carry the same identifier, method, path, status and elapsed time. Bodies, authorization headers and query strings are not logged by custom middleware. Configure log retention and redaction before ingesting real personal data; framework and provider logs also require review.

OpenAPI and Swagger UI are development-only. CORS is explicit-origin and credentialed, with unsafe requests independently checked for trusted Origin and a custom CSRF header. Authentication, authorization, rate limits and explicit proxy trust are implemented in CODEX-003; production TLS, hostnames, hardened ingress and operational access must be configured during deployment. This foundation is not safe to expose as a complete user application.

## Frontend architecture

App Router owns routing and page composition. The shell remains a server component with native mobile disclosure. Its route-group layout validates /me before rendering; an AuthProvider owns live account/loading/logout state and a client AuthGate. Shared colors, spacing, type scale, radii and layout dimensions live in `styles/globals.css`. The custom SVG icon primitive avoids a dependency for eleven simple outline icons.

`lib/api-client.ts` centralizes the origin, JSON success parsing, empty responses, HTTP ProblemDetails and network errors. Callers provide the expected TypeScript contract; runtime response validation will belong beside real feature schemas. Cancellation is passed through. The client prevents URL paths from changing the configured origin and never invents API responses. The auth client builds on it with cookie credentials, CSRF headers, coordinated refresh and one bounded retry.

Add `features/<feature>` for cohesive behavior once implemented. Add shared hooks, types, providers, accessible components, query state and form validation only when there is an actual consumer. Route-level loading/error UI should correspond to real data operations, not fake delays or simulated failures.

## Infrastructure and configuration

Compose provides PostgreSQL and Redis only; application processes run on the host. Loopback bindings support local development without exposing database ports to the LAN. Named volumes preserve state. Redis has append-only persistence and no local authentication because it is loopback-only; production Redis requires private networking, authentication and transport protection.

ASP.NET options validate connection-string structure at startup. Connectivity is checked at readiness time with bounded timeouts. `/health/live` does not test dependencies. `/health` performs a PostgreSQL query and Redis ping and reports individual statuses without details. The Redis multiplexer is created lazily and shared; it can reconnect when Redis becomes available. Authentication mutations use explicit account-row locks and transactions. Automatic execution retries were removed to avoid replay after ambiguous security-transaction commits.

Root `.env` serves Compose; ASP.NET environment/user-secrets and web `.env.local` are separate. Secrets never belong in source or `NEXT_PUBLIC_*` settings. Packages are pinned through lockfiles; database image tags follow compatible major lines, so exact image digests should be pinned for future deployment reproducibility.

## Testing philosophy

Test meaningful contracts: accessible shell structure and unavailable navigation; client success/error/network behavior; domain invariants; request ID validation; HTTP errors, CORS, liveness and unavailable dependency readiness. PostgreSQL integration tests exercise migration apply/rollback/reapply, real constraints, owner-consistent relationships, atomic history, UTC audit handling and xmin concurrency. CI explicitly enables Testcontainers; local testing can use a separately provisioned disposable PostgreSQL server. Each database test owns its own randomly named database. Test-only throwing middleware exists only in the integration assembly and never ships in the API.

## Future extension

CODEX-002 establishes account IDs and cross-owner relational integrity, but it does not authorize reads/writes. CODEX-003 implements validated JWT/cookie sessions, scoped /me and the tested OwnedData query/write boundary. Future product handlers must use that boundary. Retention/erasure policy and shared-skill administration also remain explicit future work. CODEX-004 now implements the profile/skills/account vertical slice using that boundary; see [profile](profile.md). Upgrade to .NET 10 LTS before .NET 8 support ends on November 10, 2026. Match-result persistence waits for a defined algorithm, input versioning and explanation contract; no arbitrary score columns are introduced now.

## Career profile and settings

CODEX-004 implements the first product endpoint module and profile/settings pages. Application owns DTOs and validation; ProfileService in Infrastructure coordinates existing domain behaviors through OwnedData. Four bounded native PostgreSQL arrays hold job preferences, using existing job enum names. Canonical skill insertion has a narrow shared-vocabulary policy with atomic insert-or-reuse; metadata remains owned. All edits use existing xmin concurrency. No new package, provider or deployment is required. See [profile](profile.md) for contracts, validation and migration decisions.

The shell remains a server component with small client active-navigation components. Profile/settings page-local state uses the existing authenticated client and replaces saved snapshots after mutations. Form/query libraries remain unnecessary for this scope. Email changes and real verification delivery stay deferred; password change uses CODEX-003.

## Authentication design

See [authentication](authentication.md) for the architecture decision, complete contracts, hashing/token lifetimes, replay handling, browser cookie/CSRF strategy, ownership conventions and operational limits. The existing User is preserved; Identity's password hasher and JwtBearer are reused without introducing a second IdentityUser. Infrastructure orchestrates persistent sessions/reset tokens; no production email provider or verification endpoints are claimed.

## Companies and opportunities

CODEX-005 implements the existing Company/Job entities through Application contracts, Infrastructure OpportunityService and an authenticated API endpoint module. Queries and mutations use OwnedData; bounded SQL projections and xmin protect list performance and concurrent editing. The frontend reuses the protected workspace and cookie client with dedicated forms and page-local state. Only a nullable job archive timestamp changes the schema. No new package or service is required. See [companies and jobs](companies-and-jobs.md).

## Application tracking boundary

CODEX-006 adds Application contracts, Infrastructure ApplicationService and authenticated API ApplicationEndpoints. Existing OwnedData, domain transitions, composite ownership keys and xmin remain authoritative. Next application pages reuse the auth client and shell; counts are SQL aggregates, not client-derived statistics. See [applications and pipeline](applications-and-pipeline.md).

## Deterministic skill matching

CODEX-008 reuses the existing canonical vocabulary and owner-scoped JobSkill/ResumeSkill/UserSkill relationships. Application owns pure deterministic scoring and validated contracts, Infrastructure owns snapshot queries and atomic xmin-protected set replacement, and API maps authenticated matching routes. The job detail UI exposes source selection, score contributions, evidence timestamps and manual requirement/evidence editors. There is no PDF parsing, inferred resume skill, new migration, external provider or persisted score. See [matching rules and limitations](resume-job-matching.md).

## Skills intelligence

CODEX-009 adds a read-only aggregate slice under `/api/v1/intelligence/skills` and a protected `/skills` page. PostgreSQL groups only the current owner's structured job requirements, joins owned profile/resume evidence and returns bounded pages. Matching weights and rounding come from CODEX-008. Current skill demand, profile coverage and evidence/learning priorities are explained without inventing history or skill claims. See [skills intelligence](skills-intelligence.md).
