# Authentication, security and data ownership

CODEX-003 adds first-party password authentication to the existing account model. It does not deploy the application or install an email provider. The deployment requirements below remain necessary.

## Architecture decision

Keep `CareerOS.Domain.Accounts.User` and its stable UUID as the only identity. Full ASP.NET Core Identity would require adapting its user/store semantics to the existing domain, generated email normalization and ownership graph. Instead, use its proven `PasswordHasher<User>` directly, ASP.NET Core JWT bearer validation, platform randomness and SHA-256. No password cryptography, JWT parser or signature validation is implemented here.

Application owns safe contracts, password policy, current-user and delivery abstractions. Infrastructure's AuthService orchestrates persistence and account transactions. API owns cookies, claims, CSRF, CORS and rate limits. This explicit authentication service does not change the existing product-domain architecture.

User gains nullable PasswordHash, SecurityStamp, LastLoginAtUtc and EmailVerifiedAtUtc. Existing users retain their IDs and cannot log in until recovery establishes a password; migration creates no credentials. The zero legacy stamp is harmless without a password/session. Registration creates User, an empty CareerProfile and the first refresh generation atomically, then automatically signs in. Only AccountView is returned, never an entity.

## Passwords and email

Identity V3 PasswordHasher uses salted PBKDF2-HMAC-SHA512 with 210,000 iterations and framework verification/rehash detection. Hashes contain versioned parameters. Login rehash revokes existing sessions before creating a new one. Benchmark production capacity before changing this cost. Passwords allow 15–128 UTF-16 code units, spaces and Unicode; blank input, control characters and a single repeated character are rejected. They are never trimmed. No uppercase/digit/symbol requirements are imposed. Breached-password screening is not installed.

User.NormalizeEmail calls the same validation and ASCII folding as registration. PostgreSQL's stored generated EmailNormalized and unique index remain authoritative. Addresses are trimmed and fully case-insensitive; no provider-specific plus/dot rewriting occurs. Bare ASCII addresses and punycode domains work. Internationalized local parts remain deferred: Unicode normalization must change domain, database and authentication together.

Unknown email and wrong password both return 401 `Invalid email or password.` and perform framework hash verification, using a generated dummy hash for an unknown account. Registration's generic duplicate failure differs from successful auto-authentication; availability can still be inferred through registration, bounded by throttling. Recovery responses do not disclose account existence.

## HTTP contract

All paths below are under `/api/v1/auth`. Mutations require `X-CareerOS-CSRF: 1` and an exact trusted Origin, including login/register. JSON accepts only the listed contract fields; no browser-supplied UserId is authoritative.

| Method / path         | Input                                       | Success                                                    |
| --------------------- | ------------------------------------------- | ---------------------------------------------------------- |
| POST /register        | email, password, displayName                | 200 safe account + cookies                                 |
| POST /login           | email, password                             | 200 safe account + cookies                                 |
| GET /me               | authenticated credentials                   | 200 id, email, displayName, emailVerified, createdAtUtc    |
| POST /refresh         | refresh cookie                              | 200 safe account + rotated cookies                         |
| POST /logout          | refresh cookie, optional                    | 204, revoke family and clear cookies; idempotent           |
| POST /logout-all      | authenticated credentials                   | 204, revoke every family and clear cookies                 |
| POST /change-password | currentPassword, newPassword; authenticated | 204, revoke every session/reset token, clear cookies       |
| POST /forgot-password | email                                       | 200 generic message; generic 503 when delivery unavailable |
| POST /reset-password  | token, newPassword                          | 204, consume reset tokens, revoke sessions, clear cookies  |

Safe validation errors are 400 ProblemDetails, invalid credentials/sessions 401, failed CSRF 403 and throttling 429 with Retry-After. Responses never contain password/token hashes, security stamps or raw credentials in JSON. Kestrel limits auth and ordinary requests to 16 KiB; PUT /api/v1/profile allows 128 KiB, and company/job/application POST/PUT routes allow 384 KiB for bounded Unicode descriptions and notes. Database failures fail closed. A request already authorized when revocation commits may finish; subsequent authentication checks fail.

## Access tokens

HS256 JWTs have explicit issuer/audience and a five-minute default lifetime, configurable from 1–15 minutes. Claims contain only sub (UserId), sid (family), stamp and standard validity/issuer/audience fields. No email/name/profile information is embedded. JwtBearer requires the signature, configured algorithm, issuer, audience and expiration, with zero clock skew. Synchronize production clocks.

Every authenticated request also checks the persisted User stamp/password and an unrevoked, unexpired refresh generation for that family. Logout, replay and password changes therefore invalidate access tokens on the next request. Two indexed database checks deliberately favor immediate revocation over stateless validation. A Bearer header is supported by the backend; the browser uses cookies exclusively.

## Refresh sessions and transactions

Each generation stores Id, UserId, FamilyId, TokenHash, CreatedAtUtc, ExpiresAtUtc, RevokedAtUtc and optional ReplacedByTokenId. Secrets contain 256 random bits from RandomNumberGenerator; only SHA-256 hex hashes persist. Indexed hash lookup compares high-entropy digests, not secret prefixes or plaintext passwords. No IP/user-agent metadata is collected.

Each device has an independent family. Refresh locks the User row in a PostgreSQL transaction, rereads the generation, validates it, inserts a replacement and marks the previous generation revoked/replaced atomically. Authentication mutations use the same account lock. Explicit `xmin` selection preserves the existing EF row-version mapping when locking users. Family expiry is absolute: default 30 days, configurable 1–90, never extended by rotation.

Reusing any rotated generation revokes the remaining family, even if that generation has expired. Other devices remain valid. Revoked nonrotated tokens cannot refresh. There is no replay grace window: duplicate/lost responses may require sign-in again. The frontend coalesces concurrent 401s, retries once and uses Web Locks to coordinate supporting browser tabs. After acquiring the lock, /me is checked so another tab's refresh is reused. Browsers without Web Locks retain per-tab coalescing and strict server replay protection.

Automatic EF execution retries were removed because a security transaction must not silently replay after an ambiguous commit. No automatic retry is claimed. The migration adds expiry/unique-hash/user-family indexes, restricted User FKs, an owner/family-consistent replacement FK and hash/time/replacement CHECKs. InitialDomainSchema is unchanged. Retain generations through family expiry for replay evidence. Deployment needs a retention job to purge expired families and reset tokens after its audit window; no scheduler is implemented. Replacement FKs require whole-family deletion or newest-generation-first cleanup.

## Browser storage and route protection

Production cookies are host-only: `__Host-careeros-access` at `/`, and `__Host-careeros-refresh` at `/`. Both are HttpOnly, Secure, SameSite=Strict and explicitly expire. Access uses `/` so the Next server can protect workspace rendering. Refresh also uses `/` because the \_\_Host prefix prevents sibling-subdomain cookie injection; this deliberate protection takes precedence over a narrower cookie path. Only authentication endpoints consume it, and the Next workspace check forwards only access. Development uses unprefixed names and HTTP-capable cookies exclusively in Development. There is no token storage in localStorage or sessionStorage.

The browser calls same-origin `/api/v1`. Next's rewrite uses server-only API_INTERNAL_URL; production may route `/api/v1` directly to a trusted API ingress. The workspace layout calls /me with only the access cookie and no caching. Expired/anonymous requests render no workspace and redirect through /session for browser refresh, then Overview or login. AuthProvider distinguishes loading, authenticated, anonymous and unavailable-service states; AuthGate hides content after logout/expiry. Actual name/email appear in AccountMenu. Login/register redirect authenticated users to Overview. Failed revocation does not falsely report logout success.

Reset links use `/reset-password#token=...`: a fragment is read into temporary form memory and removed from browser history. Fragments are not sent to server logs or referrers. Do not place reset secrets in query strings. Reloading after removing the fragment requires reopening the original link.

## CSRF, CORS and XSS

HttpOnly reduces theft but cannot stop same-origin XSS issuing requests. CSRF defense does not depend solely on SameSite: every unsafe API operation requires an exact allowlisted Origin and a custom header unavailable to simple cross-origin forms. Credentialed CORS allows only exact configured origins, never wildcards or subdomain regexes. Sibling-site attackers still fail Origin validation. Non-browser callers must meet the same header/origin contract. GET has no mutations; checks remain active behind the proxy. Trusted-origin control and XSS prevention remain essential.

## Recovery and verification

Reset tokens have independent 256-bit randomness, hashed storage, 30-minute expiry and single-use consumption. Only one issuance per account per minute is allowed; replacement invalidates older resets. Password reset/change invalidates all reset tokens, revokes all sessions and rotates the stamp atomically. Password change requires the current password and signs out the current device too.

IPasswordResetDelivery is the delivery extension point and must throw on failure. No production provider is installed. UnconfiguredResetDelivery reports unavailable before lookup, so every address gets the same 503 and no token is created. DevelopmentCapture requires explicit opt-in in Development; production refuses to start with it enabled. It stores at most 100 unexpired messages in memory for debugger/test consumption, never prints tokens, and exposes no capture HTTP endpoint.

Provider integration must address durable delivery, failure ambiguity, timing-based enumeration and abuse. Capture currently executes inside the account transaction: do not replace it with slow network delivery without designing an outbox/transaction boundary. No successful production email delivery is claimed.

EmailVerifiedAtUtc is the verification foundation and remains null. No verification endpoints/tokens are implemented; account creation does not prove mailbox control. A purpose-bound verification workflow is deferred with delivery integration. Do not authorize future sensitive email-dependent features from unverified addresses. Verification does not block this workspace.

## Ownership boundary

HttpCurrentUser implements Application's ICurrentUser from an authenticated, validated sub claim and rejects anonymous access. /me scopes to that ID. Logout-all/change-password never consume a browser UserId.

OwnedData supplies an explicit, reusable boundary for future product handlers: an allowlist of owned entity types, SQL UserId filtering, owner-checked add/remove and a save guard for every changed row. It covers profiles, companies, jobs, applications/history, resumes/documents, interviews, tasks, both notes, notifications and skill associations. Account/security records and shared Skill require separate policies and are rejected.

Load targets with OwnedData.Query, mutate domain behavior, then use its SaveChangesAsync. Do not inject raw DbContext into product endpoints, use guessed-ID Find, serialize entities or bypass guards with bulk/raw SQL. DbContext is reserved for narrowly reviewed auth/infrastructure operations. This is application authorization, not RLS or global EF filtering. CODEX-002 composite FKs independently protect relationships. Integration tests populate two full graphs and prove all 15 owned types are scoped and foreign writes fail.

## Rate limits and proxy trust

ASP.NET fixed windows use connection RemoteIpAddress: credentials share 10/minute (login/register/change-password/logout-all), recovery shares 5/minute (forgot/reset), refresh/logout share 60/minute. Queues are disabled; Retry-After is 60 seconds. Limits are process-local; multi-instance deployment needs ingress/distributed abuse controls. Benchmark costly password verification and bound ingress concurrency. Shared NATs share quotas.

Forwarded headers are ignored by default. Proxy:KnownProxies accepts exact trusted IPs with one forwarding hop. Configure only a proxy that overwrites untrusted X-Forwarded-For/Proto and is the sole network path to the API. Never blindly trust Next's rewrite or caller-supplied forwarding headers. Prefer a hardened production ingress routing API traffic directly, sanitizing headers, restricting direct access and applying edge limits. Without trusted forwarding, requests share the proxy IP quota deliberately. Configure TLS, AllowedHosts and private backend networking; this phase deploys none of them.

## Headers and logging

API: nosniff, no-referrer, production HSTS, and no-store/no-cache for API routes. Web: frame denial, permissions restrictions and CSP base-uri/object/frame restrictions. This is not a full nonce-based script CSP; add and test that with deployment. Development Swagger remains usable.

Custom request logs contain method/path/status/correlation/duration, never bodies/query strings/credentials/cookies. EF command/update and default exception-handler logs are suppressed because provider details may contain sensitive data. The custom exception handler records exception type/correlation only. Never enable sensitive-data logging, body/header logging or token PII diagnostics in production.

## Configuration and limitations

Set Auth**Issuer, Auth**Audience and Auth**SigningKey through environment/secret manager. SigningKey is base64 encoding of at least 32 cryptographically random bytes; there is no default or generated development key. Examples are placeholders. Set Auth**AccessTokenMinutes, Auth**RefreshTokenDays, Cors**AllowedOrigins\_\_0 (additional numbered origins as needed), and AllowedHosts. API_INTERNAL_URL is server-only and used at build time by rewrites. It is never NEXT_PUBLIC. Startup rejects missing secrets, invalid origins/lifetimes and production capture. Key rotation currently invalidates access JWTs until refresh; overlapping signing keys are not implemented.

No MFA, passkeys, social/OAuth, production email provider, erasure, other product CRUD, uploads, analytics, matching, AI or deployment is included. Upgrade .NET 8 before November 10, 2026. Verification delivery, breached-password screening, distributed throttling, retention automation, a complete script CSP and delivery outbox remain explicit follow-ups.

## References

- [Identity password configuration](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-8.0)
- [JWT bearer validation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication)
- [OWASP CSRF prevention](https://cheatsheetseries.owasp.org/cheatsheets/Cross-Site_Request_Forgery_Prevention_Cheat_Sheet.html)
