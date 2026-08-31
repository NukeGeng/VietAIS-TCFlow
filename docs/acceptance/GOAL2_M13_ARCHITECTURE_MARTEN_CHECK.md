# GOAL2 M13 Architecture Marten apply check

Status: `CONFIRMED` for the Architecture model-level migration slice only.
This artifact does not claim full M13 cutover, legacy deletion, or M14
end-to-end acceptance.

## Scope

- `ArchitectureModel` records map to a deterministic `ArchitectureModel`
  stream with a typed `ArchitectureModelCreated` event.
- Modules, module relationships, entities, data relationships, and drift
  records map to typed child events on the owning model stream.
- Child records require an explicit owning model source id; relationship
  endpoints are deterministic ids derived from explicit module/entity source
  ids.
- Source-reference and payload-hash markers make the writer repeatable and
  fail closed on hash conflicts or missing migration markers.
- The inline architecture view is updated in the same Marten transaction and
  can be rebuilt by replaying the model stream.

## Verification

`MartenProjectMigrationApplierTests.AppliesArchitectureModelAndRelationshipsOnTheModelStreamIdempotently`
passed as part of:

```text
dotnet test Tests/Migration.Tests/Migration.Tests.csproj \
  --configuration Release --no-restore -v:minimal
```

Result: `33 passed, 0 failed` on .NET 10, including the Integrations
operational and read-only Marten reconciliation checks. The test verifies typed event order,
model-stream identity, inline module/entity/relationship/drift state, and a
second apply that skips all migrated records without appending duplicates.

## Remaining M13 obligations

Full pre/post counts and domain-invariant reconciliation, isolated backup and
restore, rollback evidence, projection rebuild in a production-like stack,
and the remaining integration writers are still required before M13 can be
marked `CONFIRMED`.
