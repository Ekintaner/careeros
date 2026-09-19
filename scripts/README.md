# Developer commands

These commands work from the repository root. Existing npm scripts and the .NET CLI are sufficient; no wrapper build system is needed.

| Task                           | Command                                                                   |
| ------------------------------ | ------------------------------------------------------------------------- |
| Start dependencies             | `docker compose up -d --wait`                                             |
| Stop dependencies, retain data | `docker compose down`                                                     |
| Web development                | `npm run dev`                                                             |
| API development                | `dotnet run --project apps/api/src/CareerOS.Api`                          |
| Frontend checks                | `npm run lint`, `npm run typecheck`, `npm test`, `npm run build`          |
| Backend checks                 | `dotnet build apps/api/CareerOS.sln`, `dotnet test apps/api/CareerOS.sln` |
| Format web/docs/config         | `npm run format`                                                          |
| Check C# formatting            | `dotnet format apps/api/CareerOS.sln --verify-no-changes --no-restore`    |

See [development](../docs/development.md) for required configuration and container testing.
