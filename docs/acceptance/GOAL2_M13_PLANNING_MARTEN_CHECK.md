# GOAL2 M13 Planning Marten apply check

Status: `CONFIRMED` for the Planning model-level migration slice only. This
artifact does not claim full M13 cutover, legacy deletion, or M14 end-to-end
readiness.

## Scope

- `Plan` records map to `PlanCreated`.
- `Requirement` and `Milestone` records map to typed child events on the
  deterministic owning Plan stream.
- Child records must explicitly identify `planSourceId` (or `planId`); the
  planner never guesses a parent stream from a project id.
- Source reference and payload hash are written as event markers for safe
  replay and duplicate detection.
- `PlanCurrent` is updated by the inline projection in the same Marten
  transaction.

## Evidence

`MartenProjectMigrationApplierTests.AppliesPlanningAggregateAndChildRecordsWithDeterministicPlanStream`
uses Testcontainers PostgreSQL and verifies:

1. Plan, requirement, and milestone events are typed and ordered after the
   Plan initializer.
2. Aggregate reconstruction and the inline plan view contain the migrated
   intent and both child records.
3. Repeating the same plan appends zero duplicate events and finds the original
   source marker.
4. A requirement without an owning plan is rejected by the planner before any
   write.

The migration suite result is `28 passed, 0 failed` on .NET 10.

## Remaining M13 obligations

Remaining integration writers, pre/post count and invariant reconciliation,
isolated backup/restore, projection rebuild, cutover, rollback, and production
self-host evidence remain `PROPOSED`.
