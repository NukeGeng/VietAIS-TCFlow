# GOAL2 M13 TaskFlow Marten apply check

Status: `CONFIRMED` for the TaskFlow model-level migration slice only. This
artifact does not claim full M13 cutover, legacy deletion, or M14 end-to-end
readiness.

## Scope

- `EngineeringTask` records map to a deterministic TaskFlow stream.
- The writer appends `TaskProposed` followed by
  `TaskLifecycleReconciled`; it does not fabricate accept/start/review
  transitions or actors that are absent from the v0.1 snapshot.
- Status, assignee, AI-verification, human-review, and source-change fields are
  preserved when present and invalid values fail closed.
- `TaskVersion` and `TaskEvidence` records are preserved as typed immutable
  history on the same task stream, including snapshot JSON and source keys.
- Source reference and payload hash are written as event markers for safe
  replay and duplicate detection.
- `TaskCurrent`, task-board, and analytics projections handle the reconciliation
  event, while existing async projection registrations remain rebuildable from
  the event stream.

## Evidence

`MartenProjectMigrationApplierTests.AppliesTaskSnapshotWithoutInventingTransitionHistory`
uses Testcontainers PostgreSQL and verifies:

1. A task snapshot is mapped to the typed `TaskProposed` and
   `TaskLifecycleReconciled` events in that order.
2. Aggregate reconstruction and the inline current-task view preserve the
   migrated status, assignee, AI verification, and review state.
3. Repeating the same apply appends zero duplicate events and finds the source
   marker/hash already in the stream.
4. The task operation requires a project source identity and a supported status
   before any write.

`MartenProjectMigrationApplierTests.PreservesTaskVersionsAndEvidenceOnTheTaskStreamIdempotently`
also verifies deterministic parent-stream identity, typed version/evidence
events, projection readback, aggregate replay counters, and repeat-apply
idempotency.

The migration suite result is `28 passed, 0 failed` on .NET 10.

## Remaining M13 obligations

Remaining integration writers, pre/post count and invariant reconciliation,
isolated backup/restore, projection rebuild, cutover, rollback, and production
self-host evidence remain `PROPOSED`.
