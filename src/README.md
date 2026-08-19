# VietAIS TCFlow backend foundation

This directory contains the ASP.NET Core backend, based on FullStackHero
`2.0.4-rc`, and the .NET Aspire development host.

## Prerequisites

- .NET 9 SDK
- Docker Desktop or another Docker-compatible daemon
- Node.js `^22.18.0` or `>=24.12.0`
- npm 11 or a compatible npm release

The repository `global.json` accepts a compatible installed .NET 9 feature
band.

If multiple SDK installations exist, ensure `dotnet --version` reports a .NET
9 SDK before starting Aspire. Child projects inherit `PATH` from the AppHost.
For a keg-only Homebrew installation on Apple Silicon, for example:

```bash
export PATH="/opt/homebrew/opt/dotnet@9/bin:$PATH"
```

The Vue application pins the verified Node release in `apps/vue/.nvmrc`. Make
that Node version available in the same shell that starts Aspire so the child
frontend process inherits it.

## Install frontend dependencies

From this `src` directory:

```bash
cd apps/vue
nvm use
npm install
cd ../..
```

If a different Node version manager is used, activate a version accepted by
`apps/vue/package.json` before running `npm install` or the AppHost.

The pinned Aspire `9.0.0` baseline uses the matching official
`Aspire.Hosting.NodeJs` package for `AddNpmApp`. Newer Aspire releases renamed
that integration to `Aspire.Hosting.JavaScript`; migrate the AppHost SDK and
all Aspire integration packages together rather than mixing major versions.

## Configure local secrets

The AppHost passes credentials to the API through environment variables. Store
their values in the AppHost user-secrets store; do not add them to an
`appsettings*.json` file.

```bash
dotnet user-secrets --project aspire/Host/Host.csproj set "Parameters:jwt-key" "<at-least-32-random-characters>"
dotnet user-secrets --project aspire/Host/Host.csproj set "Parameters:hangfire-password" "<random-password>"
dotnet user-secrets --project aspire/Host/Host.csproj set "Parameters:bootstrap-admin-password" "<at-least-12-random-characters>"
```

## Run locally

```bash
dotnet run --project aspire/Host/Host.csproj
```

Aspire provisions PostgreSQL and Redis containers, waits for them to become
ready, starts the API, and then starts the Vue development server. PostgreSQL
is used both by the FullStackHero relational foundation and by the Marten-backed
Repository Intelligence module. The Aspire dashboard exposes the API and Vue
endpoints from the same application model.

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
