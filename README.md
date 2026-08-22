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
