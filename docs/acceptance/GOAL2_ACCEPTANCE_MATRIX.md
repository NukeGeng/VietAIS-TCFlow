# GOAL2 acceptance matrix

Evidence status is explicit: `CONFIRMED` means directly verified, `INFERRED`
means supported by source but not runtime-proven, and `PROPOSED` means a
remaining cutover/acceptance obligation.

| Milestone | Scope | Status | Evidence |
| --- | --- | --- | --- |
| M0 | Governing docs/inventory | CONFIRMED | docs PR #162 |
| M1 | .NET 10/FullStackHero baseline | CONFIRMED | vNext build + CI |
| M2 | Marten/Wolverine event building blocks | CONFIRMED | event-sourcing tests |
| M3 | Projects lifecycle | CONFIRMED | Projects tests/CI |
| M4 | Project AccessControl | CONFIRMED | AccessControl tests/CI |
| M5 | Planning | CONFIRMED | Planning tests/CI |
| M6 | TaskFlow reference lifecycle | CONFIRMED | TaskFlow tests/CI |
| M7 | EventStorming board | CONFIRMED | board tests/CI |
| M8 | Living Architecture reference | CONFIRMED | model tests/CI |
| M9 | Normalized RepositoryIntelligence | CONFIRMED | analysis tests/CI |
| M10 | GitHub/RabbitMQ boundary | CONFIRMED | signature test/CI; broker deployment pending |
| M11 | Platform policy/audit reference | CONFIRMED | policy test/CI; FSH Identity composition pending |
| M12 | Vue bounded-context workspace | CONFIRMED | 38 frontend tests, typecheck, lint, build |
| M13 | Migration/cutover/self-host operations | PROPOSED | deterministic planner, resumable ledger, typed Projects/AccessControl/Planning/TaskFlow/RepositoryIntelligence/EventStorming/Architecture/PlatformAdministration writers, redacted Integrations operational writer, read-only stream/document reconciliation, an isolated event-store backup/restore + async rebuild test, and a local goal2 self-host health canary are verified (see the PlatformAdministration and Integrations checks, [`GOAL2_BACKUP_RESTORE_PROJECTION_2026-08-31.json`](evidence/GOAL2_BACKUP_RESTORE_PROJECTION_2026-08-31.json), and [`GOAL2_SELF_HOST_CANARY_2026-08-31.json`](evidence/GOAL2_SELF_HOST_CANARY_2026-08-31.json)); full-context apply, semantic pre/post reconciliation, v0.1 restore, and rollback remain required |
| M14 | End-to-end GOAL2 acceptance | PROPOSED | local self-host canary confirms service startup, health, frontend routing, and unauthenticated boundaries; authenticated event/projection, broker-failure, GitHub, UI E2E, backup/restore, and rollback evidence remain required |

This matrix does not claim that the v0.1 runtime has been removed or that
production cutover has occurred.

The executable smoke checks are `deploy/self-host/goal2-preflight.sh` (secret
presence only) and `deploy/test/goal2-e2e-smoke.sh` (health plus authenticated
project boundary). Neither script prints tokens or response bodies.
