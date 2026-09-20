# Career profile, skills and account settings

CODEX-004 exposes the first authenticated product feature. `/profile` edits career information, preferences and owned skill metadata. `/settings` edits the existing User display name, shows safe account information, and uses CODEX-003 password change. No records are seeded, no external skill service is called, and email remains read-only.

## Boundaries and ownership

The existing CareerProfile, Skill and UserSkill entities remain the sources of truth. Application contains allowlisted request/response contracts and validation; Infrastructure's ProfileService coordinates EF queries and domain mutations; API's ProfileEndpoints maps authenticated `/api/v1` routes. Entities are never serialized as API responses.

Every profile/UserSkill query and write uses OwnedData with ICurrentUser from validated authentication. Requests cannot choose an owner. Unknown body properties, including UserId, audit timestamps, email and security fields, are rejected. Foreign skill relationships return 404. Account editing is a separate narrow policy: load the current User and call Rename only. Changing a name does not alter credentials or the security stamp.

Canonical skills intentionally contain shared vocabulary, not private user metadata. Authenticated users can search names and insert a validated canonical name. They cannot rename or delete canonical skills. The UI explicitly asks users to keep personal details out of skill names. Relation removal preserves vocabulary and other users' relations.

## API contracts

All routes below require authentication. All mutations also retain the existing trusted Origin/custom-header CSRF policy and no-store responses.

| Method | Route                                          | Contract                                                                           |
| ------ | ---------------------------------------------- | ---------------------------------------------------------------------------------- |
| GET    | `/api/v1/profile`                              | Career fields, preference arrays, version, server update timestamp                 |
| PUT    | `/api/v1/profile`                              | Full replacement of editable profile fields plus version                           |
| GET    | `/api/v1/profile/skills`                       | Current user's canonical names and owned proficiency/experience/version            |
| POST   | `/api/v1/profile/skills`                       | name, optional proficiency and yearsExperience; 201 with resulting relation DTO    |
| PUT    | `/api/v1/profile/skills/{skillId}`             | proficiency, yearsExperience, version                                              |
| DELETE | `/api/v1/profile/skills/{skillId}?version=...` | Delete current user's relation; 204                                                |
| GET    | `/api/v1/skills?query=...`                     | Up to 10 canonical prefix matches; fewer than two characters returns an empty list |
| GET    | `/api/v1/account`                              | Existing safe AccountView plus version                                             |
| PUT    | `/api/v1/account`                              | displayName and version only                                                       |

Existing `/api/v1/auth/me` is unchanged. Settings calls existing `/api/v1/auth/change-password` with currentPassword/newPassword after checking/refreshing the session. Invalid current credentials do not trigger mutation retries. Successful password change clears live auth state, notifies other tabs and redirects to login with a nonsecret success indicator; CODEX-003 revokes all sessions. Password values exist only in temporary form memory and are cleared on success.

Validation returns safe 400 ProblemDetails; malformed/unknown JSON fields also return a generic 400. Conflicts return 409, absent owned relations 404, and unauthenticated requests 401. Kestrel permits 128 KiB for PUT /profile to accommodate bounded Unicode summaries/preferences; auth and other routes retain the 16 KiB limit. Infrastructure failures remain generic 500 responses.

## Profile and preference representation

Existing optional fields remain: headline (200), summary (10,000), current location (200), experience (0–100 years, one decimal), and one preferred work mode. WorkMode retains OnSite/Hybrid/Remote; null means no preference. A redundant remote boolean and duplicate work-mode collection are deliberately omitted.

Four new bounded PostgreSQL native arrays hold simple value collections:

- TargetRoles: at most 10 values, 100 characters each.
- PreferredLocations: at most 10 values, 200 characters each.
- EmploymentTypes: existing Job EmploymentType enum names.
- SeniorityLevels: existing Job SeniorityLevel enum names (including Entry, Associate and Mid; no new Junior enum).

Text entries are trimmed, must be nonblank and deduplicated with OrdinalIgnoreCase while retaining the first spelling. Enum entries are validated against exact existing names, deduplicated and sorted. Empty arrays mean no preference. Arrays are replaced as one profile aggregate; they are not comma-separated data or arbitrary JSON. PostgreSQL enforces element lengths, maximum counts, nonnull elements, nonempty values, one dimension and enum membership; application validation additionally trims/deduplicates. Native arrays avoid four association tables for small scalar sets with no independent lifecycle. [Npgsql array mapping](https://www.npgsql.org/efcore/mapping/array.html).

No sensitive demographic, home-address, identity-document or birth-date fields are introduced. A legacy account with no profile reads as an honest empty DTO with version 0; only an explicit save creates its row. Registration still creates an empty profile atomically.

## Concurrency and canonical skill races

Responses expose the existing PostgreSQL xmin row version as a numeric version. Updates/deletes require the version originally read. Handlers compare it with the loaded row and retain it as EF's original concurrency value. A stale form or a change during save returns 409; it never silently overwrites another edit. No distributed lock or last-write-wins fallback is added. An unchanged save can retain its version. A missing legacy profile accepts version 0; simultaneous first saves are protected by its primary key. [Npgsql concurrency mapping](https://www.npgsql.org/efcore/modeling/concurrency.html).

Skill names retain CODEX-002 printable-ASCII normalization, including punctuation and significant internal spaces. PostgreSQL's stored generated NormalizedName and unique index remain authoritative. Within a transaction, a parameterized INSERT ... ON CONFLICT DO NOTHING inserts only a validated canonical name, with server-clock audit timestamps. The next READ COMMITTED query loads the winning canonical row. This handles concurrent creation without suppressing database uniqueness. The owned relation is then saved through OwnedData; duplicate links return 409 and the transaction rolls back. Shared raw insertion is narrowly scoped and does not bypass ownership checks on user data.

Search uses normalized, escaped literal prefix matching, an explicit varchar_pattern_ops index and Take(10). It never returns users, relation metadata or an unbounded skill catalogue. No Elasticsearch, SaaS or external dictionary is involved.

## Frontend behavior

The existing server workspace layout and AuthGate protect both pages. The shell stays a server component; small client navigation components supply correct active links/breadcrumbs. Mobile navigation includes account/sign-out controls. Profile/settings use the same cookie-based auth client, coordinated refresh and bounded retry; no new form/query dependencies were justified.

Page-local state holds fetched data and drafts. Successful mutations replace the local server snapshot and concurrency version; skill removal updates only that list; display-name changes update AuthProvider. No persistent browser cache is introduced. Fresh page mounts fetch current data. Abort signals prevent obsolete search/load results. Search is debounced; failure does not block adding a name.

Forms provide labels, text limits, optional-field hints, validation/focus feedback, disabled pending submits, error/success messages, discard actions, and unsaved indicators. Profile/account drafts warn on browser unload; client-side navigation does not automatically preserve drafts. Conflicts retain typed values and offer an explicit reload with discard confirmation. Skill conflicts require reloading before editing again. Full autosave, offline drafts and cross-tab product cache synchronization are deferred.

Account settings displays email, creation date and truthful verification status. No working verification-send action is shown because delivery is deferred. Email editing waits for a complete secure reverification flow. There are no billing, team, subscription or API-key settings.

## Migration and verification

`20260920091054_CareerProfilePreferences` was generated through EF tooling. It adds four default-empty array columns, constraints and the canonical prefix index. Existing profile/account IDs and content remain intact; the total is still 19 tables. Earlier migrations are unchanged. Rollback removes only the new preference columns/index/constraints, losing the preference values but retaining existing profile fields. Use rollback only on disposable databases or an approved recovery plan.

Tests cover input boundaries, normalization, partial-mutation prevention, real PostgreSQL persistence, authenticated/anonymous requests, two-user isolation, mass-assignment rejection, server timestamps, stale/simultaneous saves, canonical insert races, relation CRUD, bounded literal search, account rename, password-change regression, upgrade/rollback/reapply and the complete existing suite. Frontend tests exercise loading/empty/error states, validation, profile saves/conflicts/discard, skill mutations, account edits and password changes. See the phase verification report for executed results and browser limits.

## Permanent zero-cost requirement

CareerOS must remain fully usable by a single personal user without requiring a paid API, paid AI provider, paid database, paid Redis provider, paid storage provider, paid email provider, paid domain, or mandatory paid hosting service. Prefer open-source, local/self-hostable, or genuinely usable free solutions. Any paid integration must be optional, disabled by default, and must not be necessary for core functionality.

No mandatory usage-metered SaaS is permitted. CODEX-004 introduces no dependency, external provider, cloud provisioning, billing integration or deployment. Its only persistence is existing local/self-hostable PostgreSQL. Production email delivery remains unavailable rather than being secretly backed by a paid provider.
