# Companies and job opportunities

CODEX-005 makes Companies and Jobs usable through the existing authenticated workspace. All data is entered manually and persisted in the existing PostgreSQL database. No external service, package, paid API, usage-metered API, enrichment, scraping, cloud provisioning or deployment is introduced.

## Domain and architecture

Company and Job remain the CODEX-002 entities. A Job is an opportunity, not a JobApplication. Application contracts validate detached candidates before changing tracked entities; OpportunityService coordinates OwnedData and EF Core; OpportunityEndpoints handles HTTP. No repository, mediator, global tenant filter or alternate user model is added.

Companies support name, website, industry, location and description. Names are trimmed but deliberately not unique, consistent with the existing database: legitimate businesses can share names. There was no company-name uniqueness constraint to preserve or remove. No enrichment fields or logos were added.

Jobs retain title, owned company, description, location, work mode, employment type, seniority, source, source URL, decimal salary range/currency/pay period, and posting/expiry timestamps. Changing company requires another company owned by the current user. Existing application relationships remain attached to the same job identity.

## API

All routes below require authentication and retain the existing cookie, trusted Origin, custom CSRF header and no-store policy. No request accepts an owner. JSON bodies reject unknown properties.

| Method | Route                                | Behavior                                    |
| ------ | ------------------------------------ | ------------------------------------------- |
| GET    | `/api/v1/companies`                  | Bounded company summaries and job counts    |
| GET    | `/api/v1/companies/{id}`             | Company details and version                 |
| POST   | `/api/v1/companies`                  | Create; 201 with Location and safe DTO      |
| PUT    | `/api/v1/companies/{id}`             | Replace editable fields; requires version   |
| DELETE | `/api/v1/companies/{id}?version=...` | Delete an unreferenced company; 204         |
| GET    | `/api/v1/jobs`                       | Bounded opportunity summaries               |
| GET    | `/api/v1/jobs/{id}`                  | Full opportunity and company name           |
| POST   | `/api/v1/jobs`                       | Create with owned company; 201              |
| PUT    | `/api/v1/jobs/{id}`                  | Replace editable fields; requires version   |
| PUT    | `/api/v1/jobs/{id}/archive`          | `{ archived, version }`; archive or restore |
| DELETE | `/api/v1/jobs/{id}?version=...`      | Delete an unreferenced job; 204             |

Validation is 400, anonymous access 401, CSRF failures 403, absent/foreign owned resources 404, and stale versions or restricted references 409. Database exceptions never expose constraint names or private values. Product POST/PUT bodies have a 384 KiB ceiling, accommodating 50,000-character job descriptions even when JSON escapes Unicode. Profile retains 128 KiB and other/auth routes retain 16 KiB.

## Query contracts

Both lists return `items`, `page`, `pageSize`, `totalCount`, `totalPages`. Defaults: page 1, size 20. Bounds: page 1–10,000, size 1–100; invalid values are rejected. A page beyond current results is empty. Count and page queries use separate READ COMMITTED statements, so concurrent changes can briefly shift totals. This is an intentional simple offset-pagination design for a personal workspace.

Companies: `search` matches name; `industry` and `location` filter literal case-insensitive substrings. Sort: `name` (default), `newest`, `oldest`.

Jobs: `search` covers title, company name and location. Filters: `companyId`, `location`, `workMode`, `employmentType`, `seniorityLevel`, `source`, and `state` (`active` default, `archived`, `all`). Enum filters accept exact established names. Sort: `newest` (default), `oldest`, `posted` (descending, missing dates last), `company`, `title`. Every sort ends with the row UUID for deterministic ties. Text filters are bounded to 200 characters; `%`, `_` and backslash are escaped as literals. EF parameterizes values; sorting is an explicit whitelist, not dynamic SQL.

Each list executes two SQL queries: count, then projected/ordered/limited rows. No list loads descriptions, no collection is filtered after loading, and job/company data does not cause N+1 round trips. Company job counts are a correlated SQL count, including archived jobs; their links select all jobs. Existing owner/name, owner/created, owner/posted and composite company indexes remain sufficient. Substring search may scan the owner's rows; trigram/full-text infrastructure is deliberately deferred until measurements justify it. An integration test inspects executed SQL for projection, bounds and query count.

## Ownership, deletion and concurrency

Every company/job read and mutation uses OwnedData scoped to ICurrentUser from validated authentication. Company joins and counts are scoped too. A foreign ID returns the same 404 as an absent ID. Creating or changing a job to a foreign company fails before mutation; the existing `(UserId, CompanyId)` foreign key also rejects cross-owner references at the database layer. DTOs never expose owner/security state or permit audit-field assignment.

All foreign keys remain RESTRICT. Companies with jobs and jobs with applications or skill associations cannot be deleted. The database constraint protects even a dependent created concurrently with deletion. There is no cascade or implicit relationship removal. A conflict preserves the record and directs the user to keep the company or archive the job.

`ArchivedAtUtc` is nullable and stamped by the server clock. Archiving twice retains the original archive time; restoring clears it. Archiving does not mutate applications or history, and archived records remain accessible/editable by their owner. No global soft-delete filter or job state machine is introduced.

All updates, deletes and archive operations require the original numeric xmin `version`. Services compare the loaded value and use it as EF's original concurrency token. A competing save returns 409 instead of overwriting. No-op updates may retain the same version. Audit creation/update timestamps continue to come from DbContext and its server clock.

## Validation and frontend

Company limits: name 200 required, website 2048, industry 120, location 200, description 10,000. Job limits: title 200 required, description 50,000, location 200, source 120, source URL 2048. Optional blank text becomes null. Enum names use the existing domain definitions.

Salary stays numeric(14,2), nonnegative, ordered, at most two decimal places. Any amount requires a three-letter currency and Hour/Month/Year period; metadata without an amount is rejected. Currency membership/conversion is not implemented. UTC posting dates must be ordered, with expiry not before posting.

URLs accept HTTP(S), a host, no whitespace and no embedded credentials. They are metadata only: the backend never fetches them. Frontend source links are rechecked and use noopener/noreferrer. Descriptions render as plain escaped text, never HTML.

Protected routes: `/companies`, `/companies/new`, `/companies/{id}/edit`, `/jobs`, `/jobs/new`, `/jobs/{id}`, `/jobs/{id}/edit`. Existing navigation is enabled for Companies/Jobs; other future tools stay disabled. Lists have native collapsible filters, server pagination, loading/error/retry states and honest empty states. Dedicated forms avoid dialog focus complexity. Company selection searches a bounded 20-row result set; refine the search to find further matches. No arbitrary ID input is exposed.

Forms provide labels, safe validation, pending-submit disabling, cancel/discard, unload warnings and conflict draft retention with explicit reload. Posting inputs show UTC minutes; unchanged timestamps preserve original precision. All optional fields remain optional. The existing cookie client handles session refresh; no long-lived credential or product cache is stored in browser storage. Drafts are local memory, not autosaved; client-side navigation does not preserve them. List filters are page-local except company/state links; saved searches and full URL query synchronization remain deferred.

## Migration and limits

`20260920101042_CompanyAndJobLifecycle` was generated through EF tooling. It adds only nullable timestamp-with-time-zone `jobs.ArchivedAtUtc`; there are still 19 tables and no added indexes. Existing jobs start active. Rollback removes archive markers while preserving jobs; use only for disposable test databases or an approved recovery plan. The preceding migrations are unchanged.

JobApplication CRUD/pipeline, Kanban, uploads, scraping/import/parsing, browser extensions, external job APIs, AI/embeddings/matching, analytics, notifications delivery, billing and deployment are intentionally deferred. Manual entry must remain usable even if optional import features are introduced later. Core operation requires only the existing self-hostable application and PostgreSQL; existing local Redis readiness remains unchanged. See the verification report for executed checks and local preview details.
