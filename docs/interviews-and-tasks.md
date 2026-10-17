# Interviews and career tasks (CODEX-010)

This slice uses the existing Interview, InterviewNote and CareerTask tables. No migration, paid provider, subscription, new package, email delivery or calendar account is required. Core use remains local/self-hosted with permanently zero mandatory service cost.

## Ownership and concurrency

Every read, related application lookup and mutation goes through OwnedData. Parent and child note IDs must both match the current owner and each other. HTTP payloads reject unknown properties; clients cannot assign UserId, audit dates or completion timestamps. Composite foreign keys independently enforce same-owner relationships. All edit/delete operations require a nonzero current PostgreSQL xmin version; stale writes return 409 and never overwrite another edit. Simultaneous updates are tested using real PostgreSQL.

Deleting an interview with notes returns 409: users explicitly remove notes before deleting their parent. The existing restricted foreign key also protects against a concurrent note insertion. Tasks can be deleted with their current version. Applications retain their existing archive/history policy.

## HTTP contracts

All routes below are under `/api/v1`, authenticated with the existing cookies/CSRF protection and no-store policy. Validation returns 400, unavailable/foreign resources 404 and edit/relationship conflicts 409. Product request limits permit bounded Unicode notes.

| Route                                            | Behavior                                                                               |
| ------------------------------------------------ | -------------------------------------------------------------------------------------- |
| GET /interviews                                  | Bounded list: page (1–10000), pageSize (1–100), applicationId, view=upcoming/past/all  |
| GET /interviews/{id}                             | Details and current version                                                            |
| POST /interviews                                 | Create using InterviewInput                                                            |
| PUT /interviews/{id}                             | Edit using InterviewInput and version                                                  |
| DELETE /interviews/{id}?version=N                | Delete only after notes have been removed                                              |
| GET /interviews/{id}/notes?page=N                | Notes, 20 per page, newest first                                                       |
| POST /interviews/{id}/notes                      | Create plain-text note with body                                                       |
| PUT /interviews/{id}/notes/{noteId}              | Edit body with version                                                                 |
| DELETE /interviews/{id}/notes/{noteId}?version=N | Delete a specific note                                                                 |
| GET /tasks                                       | Bounded list: page/pageSize/applicationId and view=open/completed/overdue/upcoming/all |
| GET /tasks/{id}                                  | Details and current version                                                            |
| POST /tasks                                      | Create using TaskInput                                                                 |
| PUT /tasks/{id}                                  | Edit using TaskInput and version                                                       |
| DELETE /tasks/{id}?version=N                     | Delete with current version                                                            |

InterviewInput: applicationId, type (Recruiter/HR/Technical/Behavioral/HiringManager/CaseStudy/Final/Other), scheduledStartUtc, scheduledEndUtc, optional location (300), meetingUrl (2048), outcome (2000), version for edit. Start and end must be UTC and end strictly after start. Historical scheduling is allowed. The linked application cannot be changed after creation. HTTP(S) meeting links exclude embedded credentials and whitespace. Overlapping interviews are allowed; there is no external availability check or invitation delivery.

TaskInput: title (1–200), optional description (10000), dueAtUtc, priority (Low/Normal/High), completed (default false), optional applicationId and version for edit. Status is exactly Open or Completed, derived from the existing CompletedAtUtc; no invented multi-stage task model. The server sets completion time, repeat completion preserves it, and reopening clears it. Application links can be added, changed to another owned application, or removed.

Upcoming interviews include currently ongoing interviews (end > server now) and all future interviews; past means end <= now. Tasks default to open; overdue means incomplete with due < now, upcoming means incomplete and due in the inclusive interval now through now + seven days. Undated tasks remain open but belong to neither due-date view. Due-date ascending lists put undated tasks last, with High/Normal/Low ordering on ties and stable ID ordering. Pages/counts are evaluated in SQL and reflect records at query time; refresh after changes elsewhere.

## Frontend

Protected `/interviews`, `/interviews/new`, `/interviews/{id}`, `/tasks`, `/tasks/new` and `/tasks/{id}` use the existing authenticated request client. Navigation is enabled. Application detail links open filtered planning lists or preselect the application for creation. Application selectors search and paginate owned applications, including archived history. Lists have explicit loading, empty, error, refresh and pagination states.

Details support editing, explicit delete confirmation, validation, current version submission and reload after conflict. Unsaved form/note drafts warn before browser unload; failed edits preserve drafts. Notes render as plain text, including markup-like strings. Native labels, fieldsets, focusable error feedback, disabled busy controls and responsive columns support keyboard and compact screens.

Times display in the browser/device timezone, whose name is visible in the editor; the account does not acquire a new timezone preference. Entries default to device time with an explicit UTC alternative. Data is serialized as ISO UTC; editors operate at minute precision. Nonexistent daylight-saving times are rejected. Repeated local times caused by common 30/60/120-minute daylight-saving transitions require UTC entry to select an unambiguous instant. The UTC alternative also supports users working in another timezone. Date-only deadlines are not modeled.

## Limits

No reminders, notifications, emails, calendar synchronization, recurrence, interview cancellation/status model, task stages beyond open/completed, automated application-stage changes or CODEX-011 analytics are added. Outcome is optional plain text. Existing user records are never seeded or supplemented with fake appointments/tasks.
