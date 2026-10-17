# CODEX-014 performance methodology

Run date: 2026-10-17. Tests: `HardeningPerformanceTests`. Raw samples are in `performance-codex014.json`; write comparison in `skill-write-codex014.json`.

## Reproduction

Use the locked .NET dependencies and Docker, with a working PostgreSQL 17 image. PowerShell from the repository root:

```powershell
$env:RUN_CONTAINER_TESTS = 'true'
$env:CAREEROS_PERF_LARGE = 'true'
$env:CAREEROS_PERF_OUTPUT = Join-Path $PWD 'artifacts/performance.json'
$env:CAREEROS_WRITE_PERF_OUTPUT = Join-Path $PWD 'artifacts/skill-write.json'
New-Item -ItemType Directory -Force artifacts | Out-Null
dotnet test apps/api/tests/CareerOS.Tests.Integration --configuration Release --filter FullyQualifiedName~HardeningPerformanceTests
```

On Windows, a running API may lock Release binaries: stop/restart that local process around a rebuild, or initially build/test with a separate Debug output. Do not terminate unrelated processes. Default CI runs 100 and 1,000 records per owner; the opt-in above adds 5,000. No latency threshold is asserted: correctness, query budgets and response bounds are the stable regressions.

The fixture creates and drops only randomly named test databases. It never seeds the local CareerOS database. Two owners have identical cardinalities to catch accidental global counts. At each scale each owner has N companies/jobs/applications/tasks/interviews, five job-skill relations per job drawn from 20 canonical skills, ten profile skills for the measured owner, mixed priorities, completion, due dates and saved/applied states. Company names repeat across different company IDs (25 labels), role titles have ten labels. Initial status history exists; this does not represent a deep history or a large PDF workload. No resumes are seeded for this read benchmark.

After seed writes, ANALYZE refreshes statistics. Each service gets one excluded warmup then ten sequential measured calls. An EF interceptor counts executed reader/nonquery commands; transaction begin/commit are excluded. Returned projections must leave the change tracker empty. Query budgets: dashboard 7, analytics 8, intelligence 4, tasks 2. Dashboard lists are bounded to five; top analytics company/role lists to 20; task page to 20; skill page to 20 in this fixture. Owner counts are asserted at every scale.

## Environment and interpretation

Windows local host, AMD Ryzen 5 6600H (6 cores/12 logical processors), .NET SDK 8.0.425, Linux PostgreSQL 17 Testcontainers on local Docker. Pulled image digest: `postgres@sha256:f02121de6f74d30d8a94cd1d9584125e2178d7e6c377d8130112d4e52d867995`. Next.js is not part of these service timings. TestDatabase connections have pooling disabled. Timing includes service transaction/connection and real database work, but excludes HTTP, JWT checks, Next rendering and browser loading. It is not production latency, a throughput test, a cold-cache test or an Ollama benchmark. Other local build/test work ran during the final suite; scheduling noise explains non-monotonic small-scale times.

Ten samples are too few for a reliable tail distribution. The recorded nearest-rank p95 is simply the largest of those ten observations. Keep the complete sample arrays; do not present this as a statistically robust SLA. The baseline and final read runs both show constant query counts; read services were not rewritten, and differences are not attributed to a code speedup.

Final Release run, milliseconds (median / largest of ten):

| Owner records | Dashboard      | Analytics       | Skills intelligence | Tasks first page |
| ------------- | -------------- | --------------- | ------------------- | ---------------- |
| 100           | 54.76 / 61.54  | 74.49 / 90.45   | 50.52 / 104.40      | 49.10 / 78.84    |
| 1,000         | 76.50 / 127.29 | 92.18 / 149.78  | 53.48 / 78.44       | 35.69 / 49.96    |
| 5,000         | 45.40 / 48.64  | 112.77 / 118.61 | 66.80 / 86.32       | 30.91 / 32.39    |

## Measured correction

Source inspection found canonical skill insertion and lookup inside each skill-replacement loop. A separate write regression measured 10 then 100 skills against real PostgreSQL, including the existing ten names on the second replacement. Before/after runs used Debug on this host; they are single observations per size, not repeated latency distributions:

| Skills | Before commands | After commands | Before ms | After ms |
| ------ | --------------- | -------------- | --------- | -------- |
| 10     | 24              | 6              | 274.73    | 58.26    |
| 100    | 204             | 6              | 328.21    | 71.74    |

The command-count reduction is structural: one parameterized sorted INSERT/ON CONFLICT plus one canonical-name read replaces the per-skill pair. No concatenated skill text enters SQL syntax. Existing whole-set xmin/rollback/ownership semantics remain. The regression caps total commands at 12 to allow provider batching details while preventing recurrence of the 2N pattern. The final Release write run separately measured six commands at both sizes (54.99 and 63.54 ms); do not compare its latency directly with the Debug baseline.

Audit found no read N+1 in the four measured services. Existing bounded per-parent matching reads remain. Profile skills and authentication-token revocation/retention have growth limitations recorded in the security review. Aggregates, high-offset pagination, matching with unusually many canonical entries, and substring searches remain possible larger-workload costs. No speculative caching, extra index migration or framework-major rewrite is justified by this measurement alone.
