# Applications and pipeline

CODEX-006 builds on JobApplication, ApplicationStatusHistory and ApplicationNote. Application data remains in PostgreSQL; the existing User, Job, Company and Resume identities are retained. No service, paid dependency, workflow engine or drag/drop package is introduced.

## Lifecycle and historical integrity

Create from an owned existing job as Saved or Applied. There is one application per user/job, including archived applications. A direct Applied creation records only `null → Applied`; a Saved creation records `null → Saved`. Both use server time and sequence zero. Duplicate creation returns 409, with an existing application ID when found before insertion. A database uniqueness race also returns 409. The browser performs an owner-scoped lookup to offer the existing application.

Allowed transitions:

| Current status                | Destinations                                                            |
| ----------------------------- | ----------------------------------------------------------------------- |
| Saved                         | Applied, Withdrawn, Archived                                            |
| Applied                       | Assessment, Interview, Offer, Rejected, Withdrawn, Archived             |
| Assessment                    | Applied, Interview, Offer, Rejected, Withdrawn, Archived                |
| Interview                     | Applied, Assessment, Offer, Rejected, Withdrawn, Archived               |
| Offer                         | Applied, Assessment, Interview, Accepted, Rejected, Withdrawn, Archived |
| Accepted, Rejected, Withdrawn | Archived                                                                |
| Archived                      | Restore through the dedicated action                                    |

Repeated same-status requests are invalid; they do not create meaningless events. Backward movement among post-application active stages is allowed and recorded. Terminal outcomes cannot be reopened directly. Archived is retained as the existing persisted enum value rather than introducing another state column. Restore verifies the latest history event belongs to this application, matches its current revision, and records Archived → the exact pre-archive status. Restoring an archived outcome restores that outcome, not an invented active stage.

The domain generates history IDs, sequence and UTC timestamps. Existing EF append-only guards reject ordinary history edits/deletes. A single SaveChanges transaction updates the application and inserts the event; PostgreSQL xmin and the unique application/sequence index detect concurrent transitions. A loser receives 409 with no orphan history. Clients cannot supply owner, sequence, timestamps or current status through metadata editing.

AppliedAtUtc is set once on direct Applied creation or the first move into Applied. Later stage changes and restore preserve it. An explicit metadata edit may correct it to a UTC instant from 1900 through the last recorded status change, but cannot clear an established date. A never-applied application cannot acquire an applied date without entering Applied. The browser labels its calendar as UTC, permits a blank initial date to use server time, and preserves the exact stored timestamp on unrelated edits. Date corrections do not rewrite status history; this distinction matters for future analytics.

Every application has history from creation. DELETE therefore returns 409 for an owned application and recommends archive; foreign/missing IDs return 404. Nothing cascades through application history, notes, interviews or tasks. Ordinary history editing/deletion is not exposed.

## API and authorization

All routes below are relative to `/api/v1/applications`, require existing authentication, and retain trusted-Origin/custom-header CSRF enforcement, no-store responses and safe error handling. Request DTOs reject unknown JSON properties. Product POST/PUT body limits allow bounded Unicode notes; auth limits remain unchanged.

| Method | Route                         | Behavior                                                                   |
| ------ | ----------------------------- | -------------------------------------------------------------------------- |
| GET    | /                             | Paged lightweight cards                                                    |
| GET    | /summary                      | SQL-aggregated stage counts, including zero counts                         |
| GET    | /resume-options?search=       | Up to 20 owned resume metadata matches; total count for refinement         |
| POST   | /                             | Create from jobId, initialStatus, optional resumeId/source/appliedAtUtc    |
| GET    | /{id}                         | Joined job/company/resume metadata, application status, audits and version |
| PUT    | /{id}                         | Replace source/resume/applied date with required current version           |
| DELETE | /{id}                         | Historical-preservation conflict; use archive                              |
| POST   | /{id}/status                  | Validated status and version                                               |
| POST   | /{id}/archive                 | Archive with version                                                       |
| POST   | /{id}/restore                 | Restore previous status with version                                       |
| GET    | /{id}/history?page=           | Chronological history, 50 events per page                                  |
| GET    | /{id}/notes?page=             | Newest-first notes, 20 per page                                            |
| POST   | /{id}/notes                   | Create plain-text body                                                     |
| PUT    | /{id}/notes/{noteId}          | Edit body with note version                                                |
| DELETE | /{id}/notes/{noteId}?version= | Delete one note with note version                                          |

Notes trim outer whitespace, require 1–20,000 UTF-16 code units, and retain server-created/updated timestamps. React renders content as text, never HTML. Note updates/deletes have independent xmin versions. Notes remain available on archived applications. They are editable working context, not immutable status events.

ApplicationService uses OwnedData for every resource, relationship, join and write. ICurrentUser derives ownership from validated authentication. Foreign jobs, resumes, applications, histories and notes are unavailable; note lookup also includes its parent application ID. Composite ownership foreign keys remain the database defense. Resume options contain only ID/name; selecting a resume does not expose file/storage credentials or implement uploads. No resumes means an honest empty state.

## Queries and performance

List query parameters: page (1–10,000), pageSize (1–100, default 20), search (max 200), status (exact enum name), companyId, jobId, state (active/archived/all), sort (newest/oldest/updated/applied/company/title), appliedFrom and appliedTo (inclusive ordered UTC instants). Active means non-archived, including outcomes; the UI deliberately separates active stages from outcomes. Search matches job title/company name using parameterized ILIKE with literal wildcard escaping. Sorts end with application ID for deterministic ties; applied sorting places unset dates last.

Lists execute count plus LIMIT/OFFSET projection with owner-scoped joins. Cards exclude job descriptions and histories. Summary applies the same filters and uses one GROUP BY query, never all-record client aggregation. Existing owner/status/date, owner/job uniqueness and history sequence indexes are retained. No N+1 queries, external search or speculative analytics materialization is introduced. Substring search and offset pagination are suitable for a personal workspace; very large datasets may justify measured indexing/keyset work later.

The board fetches at most ten cards per displayed stage page, with independent previous/next controls. Counts come from the summary, not page lengths. Five active columns are used above 1200px; compact screens show a labelled stage selector and one vertical column. The bounded other-stage data remains available for switching. Outcomes and Archived have separate views. Filters apply to both counts and cards. Separate requests are not a transactional snapshot, so another tab can cause a temporary count/card discrepancy; Refresh pipeline reconciles them.

## Frontend and accessibility

Protected routes are /applications, /applications/new, /applications/{id}, /applications/{id}/edit. Existing auth state, refresh coalescing and API client are reused; no browser credential storage is added. Job detail checks for an existing application and shows Track application or View application. Manual creation selects a bounded server-searched existing job rather than duplicating its data. Resume search is similarly bounded.

Native labelled selects and explicit Move buttons are the deliberate alternative to drag/drop: keyboard and touch users receive the same functionality, with no extra dependency. Current cards stay in place while mutations are pending or fail. On success the board reloads real state. A stale-version response disables repeated movement and offers Reload current stage. Metadata conflicts retain drafts and require explicit reload/discard. Detail status refreshes preserve note drafts. Full-page unload warns for unsaved editors/notes; comprehensive internal-navigation blocking and cross-tab cache synchronization remain deferred.

Detail shows joined job/company, status, applied/source/resume metadata, safe HTTP(S) posting link, audit dates, chronological immutable history and notes. Status is text, not color-only. Errors are focused and safely worded, controls have pending/disabled states, and no nonfunctional interview/task controls are added. Note deletion uses the native confirmation dialog. No arbitrary HTML, file upload, external URL fetching or fake dashboard statistics are used.

## Migration and operational limits

`20260927184616_ApplicationTrackingLifecycle` was generated using EF tooling. It only broadens the initial-history CHECK to permit Saved or Applied, preserving all existing tables, enum strings, indexes and foreign keys. Table count remains 19. Earlier migrations are unchanged.

Rollback restores the Saved-only CHECK. It succeeds on compatible data, and deliberately fails transactionally if direct-Applied initial events exist. Do not rewrite historical events to force rollback. Use forward fixes or an approved backup recovery plan for such a database. Clean apply, compatible-data upgrade/rollback/reapply and full-chain lifecycle are covered by real PostgreSQL tests.

History provides future stage-entry/time-to-stage evidence. Manual applied-date corrections are not historical transition rewrites; repeated/backward stages, terminal restoration and archiving must be handled deliberately in future conversion/time metrics. No analytics dashboard or fake derived metrics are persisted.

No production deployment, cloud provisioning, paid or usage-billed dependency, AI, scraping, storage provider, uploads, interview/task management, notification delivery or billing is included. Local/self-hostable PostgreSQL and the existing stack remain sufficient. Next suggested phase: resume/document metadata and secure local-first file handling, with the same ownership, zero-cost and verification requirements.
