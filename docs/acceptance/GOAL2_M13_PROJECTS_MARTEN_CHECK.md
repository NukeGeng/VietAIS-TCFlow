# GOAL2 M13 Projects Marten apply check

Status: `CONFIRMED` for the Projects model-level slice only. This artifact does
not claim that all bounded contexts, production cutover, or self-host
operations are complete.

## Scope

The `Goal2Migration` tool was exercised with an isolated PostgreSQL 17
Testcontainers database using a redacted `Project` and `ProjectState` export.
The input was mapped to the typed Projects domain events:

```text
Project      → ProjectCreated
ProjectState → ProjectLifecycleReconciled
```

Both events use a deterministic project stream id. Each event carries a
source-reference and payload-hash marker; no legacy payload, credential, or
token is persisted in the event metadata.

## Results

| Check | Result |
| --- | ---: |
| Typed Marten event append | 2 events in one transaction |
| Inline `ProjectCurrent` projection | Reconstructed, version 2, suspended state preserved |
| Aggregate reconstruction | `ProjectAggregate` rebuilt from both events |
| Source marker readback | Project and lifecycle markers present with matching hashes |
| Repeat apply | 0 business events appended, 2 skipped |
| Unsupported `Archived` lifecycle | Rejected before any event append |
| Focused migration test suite | 23 passed, 0 failed (planner plus Projects, AccessControl, Planning, TaskFlow, and RepositoryIntelligence migration tests) |

The writer refuses unsupported bounded-context kinds, missing required fields,
an existing unmarked stream, stale ledger markers, and changed payload hashes.
The operational ledger is updated only after the Marten transaction succeeds.

## Remaining M13 gates

EventStorming, Architecture, and Integrations still require typed mappers and
reconciliation evidence. AccessControl, Planning, TaskFlow, and
RepositoryIntelligence evidence is recorded separately in
`GOAL2_M13_ACCESS_CONTROL_MARTEN_CHECK.md`,
`GOAL2_M13_PLANNING_MARTEN_CHECK.md`, and
`GOAL2_M13_TASK_FLOW_MARTEN_CHECK.md`, and
`GOAL2_M13_REPOSITORY_INTELLIGENCE_MARTEN_CHECK.md`. Full pre/post counts, isolated
backup/restore, projection rebuild rehearsal, production/self-host topology,
and rollback remain `PROPOSED`.
