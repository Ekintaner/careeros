# CODEX-001 verification

This is the historical foundation-phase record. For the current schema phase, see [CODEX-002 verification](verification-codex002.md).

Verified locally on Windows with Node 24.14.0, npm 11.9.0, .NET SDK 8.0.425 and Docker CLI 29.8.0 on September 20, 2026.

## Results

| Check                                 | Result                                                                                     |
| ------------------------------------- | ------------------------------------------------------------------------------------------ |
| Frontend dependency installation      | Passed; npm reported zero vulnerabilities                                                  |
| ESLint                                | Passed with zero warnings                                                                  |
| Type checking                         | Passed (`next typegen` and strict `tsc`)                                                   |
| Vitest                                | 5 passed across 2 test files                                                               |
| Next.js production build              | Passed; Overview and not-found routes statically rendered                                  |
| Prettier                              | Passed                                                                                     |
| .NET restore and locked restore       | Passed for all six projects                                                                |
| .NET Release build                    | Passed, zero warnings/errors                                                               |
| xUnit unit suite                      | 7 passed                                                                                   |
| In-process API integration suite      | 6 passed                                                                                   |
| Real PostgreSQL Testcontainers test   | Default run explicitly skipped; opt-in run failed because Docker engine was unavailable    |
| EF tool restore / DbContext discovery | Passed with Npgsql; no database migration or connectivity success implied                  |
| C# formatting verification            | Passed                                                                                     |
| Compose configuration validation      | Passed without starting services                                                           |
| Browser layout                        | Inspected at 1440×1000, 820×1180 and 390×844; no horizontal overflow                       |
| Browser behavior                      | Mobile disclosure opens/closes; future tools disabled; no console warnings/errors observed |
| Source review                         | No TODO/FIXME, real credentials, fake production APIs or invented user statistics found    |

## Commands executed

```sh
node --version
npm --version
dotnet --info
docker --version
npm view next version
npm view react version
npm view tailwindcss version
npm install
npm run lint
npm run typecheck
npm test
npm run build
npm run start --workspace apps/web
npm run format
npm run format:check
dotnet new sln -n CareerOS -o apps/api
dotnet sln apps/api/CareerOS.sln add <six project paths>
dotnet restore apps/api/CareerOS.sln
dotnet restore apps/api/CareerOS.sln --locked-mode
dotnet build apps/api/CareerOS.sln --configuration Release --no-restore
dotnet test apps/api/CareerOS.sln --configuration Release --no-build
dotnet format apps/api/CareerOS.sln --no-restore
dotnet format apps/api/CareerOS.sln --verify-no-changes --no-restore
dotnet tool restore
dotnet ef dbcontext info --project apps/api/src/CareerOS.Infrastructure --startup-project apps/api/src/CareerOS.Api --configuration Release --no-build
docker compose --env-file .env.example config --quiet
docker info --format '{{.ServerVersion}}'
docker desktop start
git status --short
git diff --check
```

The backend suite was also executed with `RUN_CONTAINER_TESTS=true`; 13 tests passed and the one container test failed to connect to Docker. Source searches used `rg` for TODO/FIXME, credentials, private-key/token patterns and mocked production implementations. Mock responses exist only in frontend client tests. Documentation/example password strings are placeholders. No real `.env` was created.

## Issues discovered and addressed

- PostCSS's anonymous default export triggered a lint warning; changed to a named configuration constant.
- Sandbox restrictions blocked npm registry access, .NET package restore and compiler/test child processes. Approved executions outside the sandbox restored packages, ran tests and built both applications successfully.
- ESLint 10 failed in the Next.js React plugin (`contextOrFilename.getFilename`). ESLint 9.39.5 is pinned through the lockfile for compatibility; it emits a registry deprecation notice. Upgrade when the Next.js rule dependencies support ESLint 10.
- Explicit ESM configuration removed the Vitest/Vite configuration-loader warning.
- Docker Desktop reported running after a start attempt, but neither the Linux engine pipe nor the Testcontainers engine endpoint was usable. No successful live PostgreSQL/Redis run is claimed.

## Limits

The GitHub Actions workflow must establish Linux/container verification on its runner. Local tests do not replace that result. No production deployment or real user workflow was tested or implemented. Database schema and migrations are deliberately absent. Upgrade .NET 8 before its November 10, 2026 support deadline. Image tags track compatible major lines, not immutable digests. CI and local lockfiles pin package resolution.

Git commit/push outcome and any subsequent CI result are reported in the task's final response, outside this pre-commit verification record.
