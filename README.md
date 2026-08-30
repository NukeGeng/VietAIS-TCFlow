# VietAIS TCFlow

VietAIS TCFlow is evolving from a source-aware engineering planner into an
Engineering System Intelligence and Living Architecture Platform. It connects
planning, Event Storming, architecture, data models, tasks, repositories,
source evolution, documentation, and AI-assisted workflows through explicit
domain events and traceable evidence.

## Current and target state

| State | Status | Architecture |
| --- | --- | --- |
| v0.1 baseline | `CONFIRMED` and released | .NET 9, FullStackHero `2.0.4-rc`, Vue 3, Marten documents, PostgreSQL, Redis, Aspire 9 |
| GOAL2 vNext | `PROPOSED` / in migration | .NET 10, FullStackHero v10 clean baseline, DDD, Vertical Slice, CQRS, Marten Event Store, inline and async projections, Wolverine, RabbitMQ, Aspire |

The current source tree still runs the v0.1 baseline. Target-state documents do
not imply that .NET 10, event streams, projection daemons, Wolverine, or
RabbitMQ have already been implemented.

## Governing documents

Read these in order before implementation:

1. [`GOAL2.md`](GOAL2.md) — current product evolution and target architecture.
2. [`GOAL.md`](GOAL.md) — retained v0.1 behavior and historical acceptance baseline.
3. [`PRODUCT_CONSTRAINTS.md`](PRODUCT_CONSTRAINTS.md) — product and migration risk controls.
4. [`PROJECT_PLAN.md`](PROJECT_PLAN.md) — ordered migration program and quality gates.
5. [`AGENTS.md`](AGENTS.md) — mandatory repository rules for agents.
6. [`WORKFLOW.md`](WORKFLOW.md) — implementation and verification lifecycle.
7. [`GIT_RULES.md`](GIT_RULES.md) — branch, pull request, and integration workflow.

Architecture and migration summaries are maintained in:

- [`docs/architecture/GOAL2_TARGET_ARCHITECTURE.md`](docs/architecture/GOAL2_TARGET_ARCHITECTURE.md)
- [`docs/migration/GOAL2_MIGRATION_MATRIX.md`](docs/migration/GOAL2_MIGRATION_MATRIX.md)

## Target bounded contexts

```text
PlatformAdministration
Projects
AccessControl
Planning
EventStorming
Architecture
TaskFlow
RepositoryIntelligence
Integrations
```

The migration is behavior-preserving. Every existing component must be
classified as `KEEP`, `PORT`, `REWRITE`, or `REMOVE`; none is migrated merely
because a newer framework exists.

## Current local development

The current v0.1 tree requires .NET 9, Docker, and the Node version pinned by
`src/apps/vue/.nvmrc`. Full setup, local secrets, Aspire startup, and current
API contracts are documented in [`src/README.md`](src/README.md).

```bash
export DOTNET_ROOT="/opt/homebrew/opt/dotnet@9/libexec"
export PATH="/opt/homebrew/opt/dotnet@9/bin:$DOTNET_ROOT:$PATH"
dotnet run --project src/aspire/Host/Host.csproj
```

Do not change these commands to .NET 10 until the clean vNext baseline has been
created and its runtime gate has passed.

## Current verification

```bash
dotnet restore src/VietAIS.TCFlow.sln
dotnet build src/VietAIS.TCFlow.sln --no-restore
dotnet test src/tests/RepositoryIntelligence.IntegrationTests/RepositoryIntelligence.IntegrationTests.csproj --no-build --no-restore
dotnet test src/analyzers/VietAIS.TCFlow.Analyzers.sln --no-build --no-restore
cd src/apps/vue
npm run type-check
npm run test:unit -- --run
npm run lint
npm run build
```

The v0.1 acceptance evidence remains in [`docs/acceptance/`](docs/acceptance/).
GOAL2 completion requires a new vNext acceptance matrix; historical P14 results
must not be reused as proof that event sourcing, replay, projection rebuild,
durable messaging, or bounded-context migration is complete.

## Security boundary

Never commit GitHub App secrets, private keys, webhook secrets, access tokens,
bootstrap credentials, or Codex authentication material. Domain events,
projections, audit records, logs, and integration messages must not contain
credentials or unnecessary sensitive payloads.
