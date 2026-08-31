# GOAL2 M13 EventStorming Marten apply check

Status: `CONFIRMED` for the EventStorming model-level migration slice only.
This artifact does not claim full M13 cutover, legacy deletion, or M14
end-to-end acceptance.

## Scope

- `StormingBoard` records map to a deterministic `StormingBoard` stream with a
  typed `BoardCreated` event.
- `StormingNode`, `StormingConnection`, `StormingHotspot`, and
  `StormingNodeOrder` records map to typed board child events.
- Child records require an explicit owning board source id; relationship
  endpoints are deterministic ids derived from explicit node source ids.
- Source-reference and payload-hash markers make the writer repeatable and
  fail closed on hash conflicts or missing migration markers.
- The inline board canvas is updated in the same Marten transaction and can be
  rebuilt by replaying the board stream.

## Verification

`MartenProjectMigrationApplierTests.AppliesEventStormingBoardNodesAndConnectionsOnTheBoardStreamIdempotently`
passed as part of:

```text
dotnet test Tests/Migration.Tests/Migration.Tests.csproj \
  --configuration Release --no-restore -v:minimal
```

Result: `35 passed, 0 failed` on .NET 10, including the PlatformAdministration and Integrations
operational and read-only Marten reconciliation checks. The test verifies typed event order,
board-stream identity, inline node/connection/hotspot/order state, and a
second apply that skips all migrated records without appending duplicates.

## Remaining M13 obligations

Full pre/post counts and domain-invariant reconciliation, isolated backup and
restore, rollback evidence, projection rebuild in a production-like stack,
and the remaining full-context reconciliation, backup/restore, and rollback
evidence are still required before M13 can be marked `CONFIRMED`.
