# Resume and document management (CODEX-007)

No external API, paid service, cloud account or new dependency is required. Existing document/resume tables, ownership keys, restrictive foreign keys and the unique primary-resume index are sufficient; there is no new migration.

## Use

Sign in and open `/documents` or `/resumes`. Upload PDF files up to 5 MiB, with a name and document category. Lists are owner-scoped, ordered newest first and paginated at 20 records. All document categories use the same PDF-only policy. DOCX/images/archives are deliberately unsupported in this phase.

A resume upload creates its document and resume metadata together. The first resume is primary; later uploads leave it unchanged. Select **Make primary** to change it. Primary selection never silently changes an application's selected resume. Use the existing application create/edit form to assign or remove an owned resume. Deleting a primary resume leaves no primary; select another explicitly.

An assigned resume cannot be deleted, even when all linked applications are archived. Remove those assignments first. Unassigned resumes delete their derived skill links, resume and document in one database transaction. Application history is preserved.

## HTTP contract

All routes require an authenticated session. Mutations also require the existing allowed Origin and `X-CareerOS-CSRF: 1` header.

- `GET /api/v1/documents?page=1&type=Resume`: bounded list; type is optional and must match a named DocumentType.
- `POST /api/v1/documents?filename=cv.pdf&name=My%20CV&type=Resume`: raw PDF request body and `Content-Type: application/pdf`, not multipart/base64. Returns 201 with metadata.
- `GET /api/v1/documents/{documentId}/download`: attachment, no-store, nosniff; never a public/static URL.
- `DELETE /api/v1/documents/{documentId}`: irreversible deletion, 204; 409 for a referenced resume.
- `POST /api/v1/documents/resumes/{resumeId}/primary`: 204.
- Application assignment continues through the existing versioned application create/edit endpoints and owned resume-options lookup.

Other users' IDs return 404 without disclosing ownership. DTOs expose safe download filename, name, size, MIME, category, upload time, resume ID, primary flag and assignment count. They never expose storage paths or keys. Unknown/missing on-disk files return 404, including legacy metadata without corresponding bytes.

## Storage and security

Default storage is the API account's LocalApplicationData/CareerOS/documents directory (Windows: `%LOCALAPPDATA%\CareerOS\documents`). Override with an absolute `Documents__StorageRoot` path or `Documents:StorageRoot` user secret. The root must be outside the application content directory and Git checkout. No static-file mapping may point at it. Keep the root dedicated to CareerOS and persist it across deployments. Disk capacity is an operator responsibility; there is no paid storage fallback.

The API generates random GUID keys; user filenames are never used in paths. Keys are strictly allowlisted before disk access; linked directories/files are rejected. Download names strip both slash styles, control characters and unsafe characters, limit length and prefix reserved Windows device names. Incoming MIME, `.pdf` extension, `%PDF-` header, terminal `%%EOF` marker, nonzero body and streaming 5 MiB bound are checked. MIME/signature checking is not a full PDF parser, antivirus scan or a guarantee that a PDF is harmless. Files are only downloaded as attachments; no inline preview, parsing or execution is added.

Run the API as a dedicated OS account. On Windows restrict the storage directory ACL to that account and administrators; its default per-user LocalAppData location inherits the user's permissions. On Unix the store sets directory mode 0700 and file mode 0600. Do not grant untrusted local users write access to the root or its parents: path checks cannot prevent a privileged local process replacing files between check and open.

Database transactions and per-owner PostgreSQL advisory locks serialize upload/primary/delete. The existing foreign keys protect concurrent application assignments. Bytes are durably written before metadata commit; normal database failures remove their upload. A process crash or uncertain commit may leave an orphan file. Deletion commits metadata before deleting bytes, so a filesystem failure cannot make a surviving database reference lose its file. Failed byte cleanup is logged by document ID and leaves a private inaccessible orphan. PostgreSQL and the filesystem do not share a distributed transaction.

## Backup and restore

Back up **both** PostgreSQL and the complete configured storage root as one pair. A database-only backup cannot recover files. Protect backups like the original documents, and keep encryption keys/secrets outside source control.

1. Stop the API (and every writer/maintenance process); leave PostgreSQL available. Do not delete Docker volumes.
2. Record the current Git commit, configured storage root and database migration version.
3. Create a PostgreSQL custom-format dump using `pg_dump -Fc` with your configured connection. Use a password prompt or protected environment, never a password in a committed script.
4. Copy the whole storage root, retaining the generated filenames, into the same timestamped backup directory. Verify file count and hashes against the source before restarting the API. A filesystem snapshot is also suitable while writes are stopped.
5. Restart the API and verify readiness plus an authenticated download.

To restore, stop the API again; restore the dump with `pg_restore` into a new empty PostgreSQL database; copy the paired files to a dedicated root outside Git/web directories; restore OS permissions; point connection and storage configuration at the restored pair. Run the matching code/migration version, start the API, verify readiness, sign in and test list/download/primary/application assignment. Confirm ownership with a second user. Keep the previous database and root until verification is complete. Do not restore over a live database/root or mix different backup timestamps.

For orphan reconciliation, stop every writer and take a backup first. Export `documents.StorageKey` from the database and compare it with allowlisted GUID `.pdf` filenames in the dedicated root. Quarantine unreferenced files outside the live root; investigate missing referenced files and recover them from the matching backup. Never delete files merely based on age while the API is running. No automatic destructive cleanup job is included.
