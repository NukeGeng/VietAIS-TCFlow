# VietAIS TCFlow backend foundation

This directory contains the ASP.NET Core backend, based on FullStackHero
`2.0.4-rc`, and the .NET Aspire development host.

## Prerequisites

- .NET 9 SDK
- Docker Desktop or another Docker-compatible daemon

The repository `global.json` accepts a compatible installed .NET 9 feature
band.

If multiple SDK installations exist, ensure `dotnet --version` reports a .NET
9 SDK before starting Aspire. Child projects inherit `PATH` from the AppHost.
For a keg-only Homebrew installation on Apple Silicon, for example:

```bash
export PATH="/opt/homebrew/opt/dotnet@9/bin:$PATH"
```

## Configure local secrets

The AppHost passes credentials to the API through environment variables. Store
their values in the AppHost user-secrets store; do not add them to an
`appsettings*.json` file.

```bash
dotnet user-secrets --project aspire/host/Host.csproj set "Parameters:jwt-key" "<at-least-32-random-characters>"
dotnet user-secrets --project aspire/host/Host.csproj set "Parameters:hangfire-password" "<random-password>"
dotnet user-secrets --project aspire/host/Host.csproj set "Parameters:bootstrap-admin-password" "<at-least-12-random-characters>"
```

## Run locally

```bash
dotnet run --project aspire/host/Host.csproj
```

Aspire provisions PostgreSQL and Redis containers, waits for them to become
ready, and then starts the API. PostgreSQL is used both by the FullStackHero
relational foundation and by the Marten-backed Repository Intelligence module.

## Verify

```bash
dotnet restore VietAIS.TCFlow.sln
dotnet build VietAIS.TCFlow.sln --no-restore
dotnet test tests/RepositoryIntelligence.IntegrationTests/RepositoryIntelligence.IntegrationTests.csproj --no-build --no-restore
dotnet list VietAIS.TCFlow.sln package --vulnerable --include-transitive
```

The Marten integration test starts an isolated PostgreSQL container and verifies
an explicit `IDocumentSession.SaveChangesAsync` write followed by an
`IQuerySession` read.
