# Release operations

This release supports local self-hosting with Docker PostgreSQL/Redis and host-run API/web. No public host is created. See README and development.md for executable install/configuration commands.

## Clean installation and migrations

Use a new checkout, private `.env`, and unused host ports. Install locked npm/NuGet dependencies and local EF tools. Start Compose, configure user-secrets, set `ASPNETCORE_ENVIRONMENT=Development`, then apply `dotnet ef database update --project apps/api/src/CareerOS.Infrastructure --startup-project apps/api/src/CareerOS.Api`. Startup does not migrate or seed. The five migrations end at `20260927184616_ApplicationTrackingLifecycle`; CODEX-014/015 add no migration.

Check model compatibility using `dotnet ef migrations has-pending-model-changes` with the same project flags. For a reviewed deployment SQL script use `dotnet ef migrations script --idempotent` with those flags. Review before applying. Never edit already-applied migrations. Register a new account through the web; there are no default credentials.

For an isolated infrastructure check, set process variables POSTGRES_PORT=55432 and REDIS_PORT=56379, choose a generated POSTGRES_PASSWORD, and run `docker compose -p careeros-release-check up -d --wait`. This project has separate named volumes. API connection strings must use those same ports. Never reuse a user's volume for a clean-install test. `docker compose down` retains data; do not add `--volumes` to routine commands.

## Safe backup and restore

Back up PostgreSQL **and** the private document root as a quiescent pair. See [storage contract](documents-and-resumes.md#backup-and-restore). Do not put backup files under Git or the web root.

1. Stop API and all other writers; retain database/Redis services. Record commit, migration IDs, storage root and configuration setting names. Keep secret values separately protected.
2. Create a new private timestamped backup directory. Dump through PostgreSQL 17 tools using a password prompt or protected environment. In Compose, `docker compose exec -T postgres pg_dump -U careeros -d careeros -Fc -f /tmp/careeros-backup.dump` followed by `docker compose cp postgres:/tmp/careeros-backup.dump <private-backup-directory>/database.dump` avoids PowerShell binary-output redirection. Substitute the actual DB/user. Check each exit code.
3. Copy the entire private storage root into the same backup directory while writers remain stopped. Preserve generated filenames. Compare file count and SHA-256 hashes before restarting. Encrypt/protect the pair, restrict access, and keep a second copy on an operator-selected medium. Do not upload automatically.
4. Restart and verify readiness plus authenticated document download. A successful dump alone is not a restore test.

Restore into a **new empty database and new dedicated private root**. Copy the dump into the container and run `pg_restore --exit-on-error --no-owner --no-acl -U <role> -d <new-database> <dump-path>`. Retain the original database/root. Restore paired files and OS permissions; configure the API to use the restored pair and matching code revision. Verify migrations, sign-in, lists, PDF hashes/download, resume assignment and denial to another user. Only switch normal usage after those checks. Do not restore over live data or mix timestamps. The disposable integration restore drill is evidence of the procedure's application semantics, not proof that an operator's real backup is recoverable.

Redis is not the authoritative document/product store. Preserve it during ordinary restarts. Backup secrets and OS permissions require their own operator plan. Keep database dumps/private PDFs out of logs, screenshots and commits. Remove temporary dump files from containers during operator maintenance after securing and verifying the backup.

## Upgrade and recovery

Record the current revision, stop writers and take the verified paired backup first. Install locked dependencies, build and test the candidate revision. Review migration SQL before applying; this release has no new schema change. Keep the previous application binaries/configuration and original data pair until verification completes. Start API, check readiness and core authenticated flows, then web. Restart web after configuration/build changes to avoid stale chunks.

If application startup fails before any data/schema changes, restore previous code/configuration. If a migration or candidate application wrote incompatible data, recover the complete previous database/files pair into new locations and run its matching revision. Do not blindly run migration rollback: it can delete data. Recovery time and recovery-point objectives have not been measured for user data. Test operational recovery periodically on separate storage.

## Deployment options

Checked 2026-10-17; provider terms can change. No account/resource was created and no expense authorized.

| Option                                                | Assessment                                                                                                                                                                                                                             |
| ----------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Existing Windows/Linux computer + Docker dependencies | Reproducible default, no mandatory subscription. Requires uptime, disk monitoring, private permissions and backups. Current HTTP loopback configuration is local only.                                                                 |
| Render free services                                  | Suitable for experiments, not a complete durable CareerOS deployment: free web services sleep, local filesystem is ephemeral, and free PostgreSQL expires after 30 days. Private PDF durability and all services need a separate plan. |
| Operator-provided server                              | Possible after ingress/security/storage work; no server purchase or provisioning is part of this release.                                                                                                                              |

Source: [Render free-service limits](https://render.com/docs/free), [filesystem/idle behavior](https://render.com/docs/faq). Do not treat an expiring database trial as permanent zero-cost hosting. Static-only hosting cannot run the ASP.NET API or private PDF storage.

Before public hosting: upgrade .NET to a supported LTS, provide HTTPS and Secure cookies, explicit frontend origins/AllowedHosts, trusted proxy IPs, secret management, durable private storage, backup/restore monitoring, recovery delivery, quotas/retention and distributed abuse controls as needed. Current rate limits are per process; PDF checks are not malware scanning. No full penetration test, availability SLA or public deployment is claimed.
