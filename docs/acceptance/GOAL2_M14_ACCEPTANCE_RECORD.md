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

## Required runtime evidence before marking M14 complete

| Required check | Current status | Required artifact |
| --- | --- | --- |
| Aspire starts PostgreSQL, API, async daemon, and UI together | `PROPOSED` | Timestamped startup/health transcript from an isolated environment |
| Event append, inline visibility, async convergence, replay, and rebuild | `INFERRED` | Test output showing empty-projection rebuild and daemon convergence |
| Optimistic concurrency and duplicate durable delivery | `INFERRED` | Concurrent command and duplicate-message test output |
| RabbitMQ routing, retry, dead-letter, and broker outage behavior | `PROPOSED` | Broker smoke/failure transcript with queue and dead-letter counts |
| FullStackHero Identity 401/403/success paths and project scope | `PROPOSED` | Authenticated API transcript using non-production test identities |
| Platform and AI mutation audit trail | `INFERRED` | Queryable audit records with correlation/actor metadata |
| Live GitHub App installation, private-repository ingestion, and webhook | `PROPOSED` | Redacted delivery/analysis evidence; no token or private key in logs |
| End-to-end Vue workflows against the vNext API | `INFERRED` | Browser/API workflow artifact from the same deployed build |
| v0.1 migration idempotency, backup restore, and rollback | `PROPOSED` | Dry-run report, restored database checksums/counts, and rollback record |

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
