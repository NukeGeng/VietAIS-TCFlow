# GOAL2 M13 Marten reconciliation check

Status: `CONFIRMED` for the read-only event-stream marker/hash check. This
artifact does not claim that the complete migration, operational-document
reconciliation, backup/restore, or rollback gates have passed.

## Scope

`MartenMigrationReconciler` consumes the same schema-versioned migration plan
as the typed Marten apply. Against an initialized target Event Store it reads
the expected stream identities and migration metadata only. It does not apply
schema changes, append events, update projections, or mutate operational
documents.

The command is:

```bash
dotnet run --project src/vnext/Tools/Goal2Migration/Goal2Migration.csproj -- \
  --input /path/to/v0.1-export.v1.json \
  --output /path/to/goal2-marten-reconciliation.json \
  --reconcile-marten \
  --connection "$TCFLOW_MARTEN_CONNECTION"
```

Exit code `0` means every expected event-stream source marker and payload hash,
and every supported Integrations operational document, was found exactly once.
Exit code `3` reports a reconciliation failure. Unsupported operational kinds
fail closed because they require their own owning-context mapper.

## Verification

| Check | Result |
| --- | ---: |
| Applied typed Project stream, then reconciled source marker/hash | passed |
| Applied redacted GitHub delivery document, then reconciled source/hash | passed |
| Missing stream and changed payload hash reported without writes | passed |
| `MartenMigrationReconcilerTests` | 3 passed, 0 failed |
| Full `Migration.Tests` suite | 35 passed, 0 failed |
| Migration tool Release build | 0 warnings, 0 errors |

## Remaining M13 evidence

This check proves stream/document-level marker/hash presence and duplicate
detection for the tested typed writers. It does not prove semantic pre/post
record counts, business invariants, isolated backup/restore, projection rebuild
after restore, or rollback rehearsal. Those artifacts remain required before
M13 can move from `PROPOSED` to `CONFIRMED`.
