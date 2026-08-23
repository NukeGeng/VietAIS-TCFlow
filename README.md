# VietAIS TCFlow

VietAIS TCFlow is a source-aware engineering planner that turns repository
evidence and source changes into explainable engineering impact and tasks.

The product baseline is Vue 3 + TypeScript + Vite, ASP.NET Core on
FullStackHero `2.0.4-rc`, Marten + PostgreSQL, and .NET Aspire.

## Governing documents

- [`GOAL.md`](GOAL.md) — product and architecture source of truth
- [`PRODUCT_CONSTRAINTS.md`](PRODUCT_CONSTRAINTS.md) — product risk controls
- [`PROJECT_PLAN.md`](PROJECT_PLAN.md) — implementation order and evidence gates
- [`AGENTS.md`](AGENTS.md) — repository rules for implementation agents
- [`WORKFLOW.md`](WORKFLOW.md) — mandatory implementation lifecycle
- [`GIT_RULES.md`](GIT_RULES.md) — branch, pull request, and merge workflow

The integrated baseline includes the FullStackHero backend, PostgreSQL, Redis,
Marten document persistence, the Vue product workspace, and one .NET Aspire
AppHost for local orchestration.

## System administration contracts

The root-tenant `Admin` role receives TCFlow's system permissions independently
from project membership. System Admin access does not grant Project Owner access.

- `GET /api/v1/system/projects` requires `project.inspect`.
- `PUT /api/v1/system/projects/{projectId}/status` requires `project.suspend`
  and accepts only `Active` or `Suspended`.
- `GET /api/v1/system/permission-definitions` requires
  `permission-definition.manage`.
- `GET /api/v1/system/audit` requires `system-audit.view`.

Suspending a project removes its project-scoped effective grants until a System
Admin activates it again. Each lifecycle change records the System Admin actor,
target, before state, and after state in the Marten audit store. User activation
and suspension remains protected by `Permissions.Users.Update`.
Assigning platform roles to a user requires `Permissions.UserRoles.Update`.

## GitHub App for private repositories

Create a GitHub App owned by the account or organization that will install it.
Configure both the **Setup URL** and **Callback URL** as
`http://localhost:5173/github/callback`. Give it read-only **Contents** and
**Metadata** repository permissions, subscribe to `push` and `pull_request`, and
set its webhook URL to the public HTTPS address that forwards to
`/api/v1/github/webhooks`.

Store local credentials in the Aspire AppHost user-secrets store; never add the
GitHub client secret, private key, or webhook secret to source control:

```bash
dotnet user-secrets set --project src/aspire/Host/Host.csproj "Parameters:github-app-id" "YOUR_APP_ID"
dotnet user-secrets set --project src/aspire/Host/Host.csproj "Parameters:github-app-slug" "YOUR_APP_SLUG"
dotnet user-secrets set --project src/aspire/Host/Host.csproj "Parameters:github-client-id" "YOUR_CLIENT_ID"
dotnet user-secrets set --project src/aspire/Host/Host.csproj "Parameters:github-client-secret" "YOUR_CLIENT_SECRET"
dotnet user-secrets set --project src/aspire/Host/Host.csproj "Parameters:github-private-key-base64" "BASE64_ENCODED_PEM"
dotnet user-secrets set --project src/aspire/Host/Host.csproj "Parameters:github-webhook-secret" "YOUR_WEBHOOK_SECRET"
```

The OAuth user token and installation token are short-lived process memory only.
TCFlow persists the verified installation and selected repository identities,
but never persists those tokens or includes them in audit records.

Technology-neutral analyzer contracts, deterministic analyzers,
knowledge/governance engines, and bounded AI task reconciliation are documented
in [`src/analyzers/README.md`](src/analyzers/README.md).

## Analyzer verification

With the repository's .NET 9 SDK available:

```bash
dotnet restore src/analyzers/VietAIS.TCFlow.Analyzers.sln
dotnet build src/analyzers/VietAIS.TCFlow.Analyzers.sln --no-restore
dotnet test src/analyzers/VietAIS.TCFlow.Analyzers.sln --no-build --no-restore
```
