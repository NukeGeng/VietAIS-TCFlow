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

## Project authorization API

Project authorization is enforced independently from FullStackHero platform
authorization. System administrators do not implicitly receive project-owner
permissions: access to a project API requires an active project membership and
a matching project role grant.

The version 1 endpoints are:

| Method | Route | Required project permission |
| --- | --- | --- |
| `GET` | `/api/v1/projects/{projectId}/permission-definitions` | `role.view` |
| `POST` | `/api/v1/projects/{projectId}/roles` | `role.create` |
| `PUT` | `/api/v1/projects/{projectId}/roles/{roleId}/permissions` | `role.update` |
| `PUT` | `/api/v1/projects/{projectId}/members/{memberId}/roles` | `member.role.assign` |
| `GET` | `/api/v1/projects/{projectId}/members/{memberId}/effective-permissions` | `role.view` |
| `PUT` | `/api/v1/projects/{projectId}/ai-policy` | `ai.policy.update` |
| `GET` | `/api/v1/projects/{projectId}/authority-policy` | `authority.view` |
| `PUT` | `/api/v1/projects/{projectId}/authority-policy` | `authority.update` |
| `GET` | `/api/v1/projects/{projectId}/convention-profile` | `convention.view` |
| `PUT` | `/api/v1/projects/{projectId}/convention-profile` | `convention.update` |
| `POST` | `/api/v1/projects/{projectId}/ownership-transfers` | `project.ownership.transfer` |
| `GET` | `/api/v1/projects/{projectId}/audit` | `audit.view` |

Permission codes and their system/project classification are system-defined.
Project roles may only select project permission definitions. Grants combine a
resource scope (`project`, `repository`, `component`, `own`, `assigned`, and so
on) with optional component scopes (`frontend`, `backend`, `database`, or
`tests`). Effective-permission responses include the granting role and scopes.

Role, permission, member-role, AI-policy, authority-policy, convention-profile,
and ownership mutations store their audit record in the same Marten
`IDocumentSession` transaction. Authority selects the source of truth for each
knowledge kind and never grants actor permissions. AI actor permissions are
additionally capped by the configured progressive trust level.

## Project management API

Creating `POST /api/v1/projects` requires authentication but no pre-existing
project permission. One Marten transaction creates the project, active state,
primary Owner membership and system-defined Owner role, default authority and
convention records, default AI policy, and audit record.

Project-scoped version 1 routes include:

| Method | Route | Required project permission |
| --- | --- | --- |
| `GET` | `/api/v1/projects` | Filters the caller's memberships by `project.view` |
| `GET` | `/api/v1/projects/{projectId}` | `project.view` |
| `POST` | `/api/v1/projects/{projectId}/repositories` | `repository.create` |
| `GET` | `/api/v1/projects/{projectId}/repositories` | `repository.view` with resource/component scope |
| `POST` | `/api/v1/projects/{projectId}/components` | `component.create` |
| `POST` | `/api/v1/projects/{projectId}/features` | `feature.create` |
| `POST` | `/api/v1/projects/{projectId}/tasks` | `task.create` |
| `GET` | `/api/v1/projects/{projectId}/tasks` | `task.view` with project/repository/component/own/assigned scope |
| `GET` | `/api/v1/projects/{projectId}/tasks/{taskId}` | `task.view` with task scope |
| `PUT` | `/api/v1/projects/{projectId}/tasks/{taskId}/status` | `task.status.update`, `task.approve`, or `task.reject` according to target state |
| `PUT` | `/api/v1/projects/{projectId}/tasks/{taskId}/assignment` | `task.assign` |
| `POST` | `/api/v1/projects/{projectId}/tasks/{taskId}/reviews` | `task.review` |
| `POST` | `/api/v1/projects/{projectId}/tasks/{taskId}/evidence` | `task.update` |
| `GET` | `/api/v1/projects/{projectId}/tasks/{taskId}/history` | `task.view` with task scope |

The lifecycle is `Upcoming → In Progress → Ready For Review → Completed`, with
`Blocked`, `Rejected`, and `Cancelled` branches. Invalid transitions are
rejected. Completion requires explicit human approval; AI verification is a
separate state and never implies human approval. Every task mutation stores a
typed version snapshot and audit record atomically. Source-generated tasks can
trace back to change, artifact, evidence, and impact documents.
