# GOAL2 M14 end-to-end acceptance record

Status: `PROPOSED`. This record separates evidence that is already available
from checks that require an isolated, credentialed runtime. It does not claim
that the v0.1 runtime has been cut over or removed.

## Evidence available in repository and CI

| Acceptance area | Evidence | Status |
| --- | --- | --- |
| Governing documentation and migration inventory | Docs PR #162 and the M13 cutover runbook | `CONFIRMED` |
| .NET 10/vNext solution, bounded-context tests, and backend tests | Quality-gates workflow run for PR #176; all five jobs passed | `CONFIRMED` |
| Vue typecheck, tests, lint, and production build | Quality-gates workflow run for PR #176; frontend gate passed | `CONFIRMED` |
| Self-host compose validation and smoke test | Quality-gates workflow run for PR #176; self-host gate passed | `CONFIRMED` |
| Deterministic aggregate decisions and invalid transitions | M2–M11 module test suites | `CONFIRMED` |
| GitHub webhook signature rejection and delivery deduplication | M10 integration test and sanitized delivery contract | `CONFIRMED` |
| Analyzer facts, evidence, and historical task reconciliation | Existing analyzer/P14 evidence and M9 source contracts | `CONFIRMED` for the retained v0.1 path; `INFERRED` for complete vNext parity |
| Versioned migration planner, resumable ledger, typed Projects/AccessControl/Planning/TaskFlow/RepositoryIntelligence/EventStorming/Architecture/PlatformAdministration writers, redacted Integrations operational writer, duplicate protection, and read-only Marten stream/document reconciliation | `Goal2MigrationPlannerTests`, `MartenProjectMigrationApplierTests`, `MartenOperationalMigrationApplierTests`, `MartenMigrationReconcilerTests`, [`GOAL2_M13_LEDGER_CHECK.md`](GOAL2_M13_LEDGER_CHECK.md), [`GOAL2_M13_RECONCILIATION_CHECK.md`](GOAL2_M13_RECONCILIATION_CHECK.md), [`GOAL2_M13_INTEGRATIONS_MARTEN_CHECK.md`](GOAL2_M13_INTEGRATIONS_MARTEN_CHECK.md), [`GOAL2_M13_PLATFORM_ADMINISTRATION_MARTEN_CHECK.md`](GOAL2_M13_PLATFORM_ADMINISTRATION_MARTEN_CHECK.md), the bounded-context M13 checks, and the M13 runbook commands | `CONFIRMED` for the model-level event slices, platform configuration streams, redacted Integrations metadata, operational checkpointing, and source/hash checks; semantic full-context reconciliation remains `PROPOSED` |
| Isolated vNext API startup and identity boundary | `docs/acceptance/GOAL2_VNEXT_AUTH_RUNTIME_CHECK.md` (fresh PostgreSQL database; redacted local transcript) | `CONFIRMED` for this isolated API check; Aspire, RabbitMQ, and production deployment remain `PROPOSED` |
| Local GOAL2 self-host canary | [`GOAL2_SELF_HOST_CANARY_2026-08-31.json`](evidence/GOAL2_SELF_HOST_CANARY_2026-08-31.json) | `CONFIRMED` for local service health, frontend routing, RabbitMQ diagnostics ping, and unauthenticated 401 boundaries; it is not production or authenticated end-to-end evidence |
| Aspire composition declares RabbitMQ for integration events and keeps Marten async processing local | `src/vnext/Host/FSH.Starter.AppHost/AppHost.cs` resource/environment wiring; no runtime transcript yet | `INFERRED` |
| Isolated PostgreSQL backup/restore with projection rebuild | [`GOAL2_BACKUP_RESTORE_PROJECTION_2026-08-31.json`](evidence/GOAL2_BACKUP_RESTORE_PROJECTION_2026-08-31.json), `EventStoreIntegrationTests.BackupRestorePreservesEventHistoryAndRebuildsAsyncProjection` | `CONFIRMED` for the Projects event-store slice; full v0.1 migration restore and rollback remain `PROPOSED` |

## Required runtime evidence before marking M14 complete

| Required check | Current status | Required artifact |
| --- | --- | --- |
| Aspire starts PostgreSQL, API, async daemon, and UI together | `PROPOSED` | Timestamped startup/health transcript from an isolated environment; the self-host canary is a separate local Compose check |
| Event append, inline visibility, async convergence, replay, and rebuild | `CONFIRMED` for isolated vNext event/projection integration tests; full authenticated end-to-end workflow remains `PROPOSED` | `EventStoreIntegrationTests` output, including the backup/restore rebuild artifact |
| Optimistic concurrency and duplicate durable delivery | `INFERRED` | Concurrent command and duplicate-message test output |
| RabbitMQ routing, retry, dead-letter, and broker outage behavior | `PROPOSED` | Broker smoke/failure transcript with queue and dead-letter counts |
| FullStackHero Identity 401/403/success paths and project scope | `CONFIRMED` for isolated vNext API 401/403/success and inline/async project reads; project-scope matrix remains `PROPOSED` | `docs/acceptance/GOAL2_VNEXT_AUTH_RUNTIME_CHECK.md` plus a multi-tenant/project authorization artifact |
| Platform and AI mutation audit trail | `INFERRED` | Queryable audit records with correlation/actor metadata |
| Live GitHub App installation, private-repository ingestion, and webhook | `PROPOSED` | Redacted delivery/analysis evidence; no token or private key in logs |
| End-to-end Vue workflows against the vNext API | `INFERRED` | Browser/API workflow artifact from the same deployed build |
| v0.1 migration idempotency, backup restore, and rollback | `PROPOSED` | Model-level idempotency and isolated event-store restore are confirmed separately; restored v0.1 counts/checksums and rollback record are still required |

## Execution order

1. Run `deploy/self-host/goal2-preflight.sh` through the secret manager without
   printing values.
2. Restore a v0.1 backup into an isolated PostgreSQL instance and execute the
   dry-run/idempotent migration described in
   `docs/migration/GOAL2_CUTOVER_RUNBOOK.md`.
3. Start the same commit through Aspire and run
   `deploy/test/goal2-e2e-smoke.sh` with a short-lived test token.
4. Exercise projection rebuild, Wolverine inbox/outbox retry/dead-letter, and
   RabbitMQ outage handling; capture only counts, positions, and correlation
   identifiers.
5. Run the authenticated private GitHub repository canary and compare source,
   evidence, task, permission, and projection counts.
6. Mark the rows above `CONFIRMED` only after the artifacts are attached to the
   release record and the rollback window is accepted.

Until these artifacts exist, M14 remains `PROPOSED` and the legacy runtime must
remain available as the rollback reference.
