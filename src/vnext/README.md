# GOAL2 vNext baseline

This folder is the migration workspace for the GOAL2 architecture. It is a
source-owned FullStackHero `10.0.0` / .NET 10 baseline kept beside the current
v0.1 runtime (`src/api`, `src/analyzers`, and `src/apps`). The two runtimes are
not interchangeable yet; migration and cutover are tracked in
[`docs/migration/GOAL2_MIGRATION_MATRIX.md`](../../docs/migration/GOAL2_MIGRATION_MATRIX.md).

## Current slice

`Modules/Projects` is the first bounded context in this baseline:

- Commands are handled by Wolverine and append events to a Marten event stream.
- `ProjectCurrent` is an inline projection for immediate reads.
- `ProjectPortfolioSummary` is an async projection processed by the Marten async daemon.
- The API host is `Host/VietAIS.TCFlow.Api`.

This is an implementation slice, not a claim that the complete GOAL2 system is
finished. Access control, planning/task contexts, integrations, frontend
cutover, replay tooling, and production deployment remain on the migration
plan.

## Local verification

The nested `global.json` selects .NET 10. From this directory:

```bash
../../.tools/dotnet10/dotnet restore FSH.Starter.slnx
../../.tools/dotnet10/dotnet build FSH.Starter.slnx --configuration Release
../../.tools/dotnet10/dotnet test Tests/Projects.Tests/Projects.Tests.csproj --configuration Release
```

The API needs a PostgreSQL connection in `ConnectionStrings:marten`. In
development, Wolverine uses runtime compilation; production deployment must
switch to pre-generated/static Wolverine code as part of the M13 cutover gate.
