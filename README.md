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

The foundation now includes the FullStackHero backend, PostgreSQL, Redis,
Marten document persistence, a Vue product shell, and one .NET Aspire AppHost
for local orchestration. Later product milestones remain governed by the
dependency order and evidence gates in `PROJECT_PLAN.md`.
