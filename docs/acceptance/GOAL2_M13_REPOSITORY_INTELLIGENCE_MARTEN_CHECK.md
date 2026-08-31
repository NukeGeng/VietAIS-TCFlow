# GOAL2 M13 RepositoryIntelligence Marten apply check

Status: `CONFIRMED` for the RepositoryIntelligence model-level migration slice
only. This artifact does not claim full M13 cutover, legacy deletion, or M14
end-to-end readiness.

## Scope

- `AnalysisRun` records map to `AnalysisStarted` on a deterministic analysis
  stream owned by the migrated project.
- `SourceArtifact` records map to typed `ArtifactObserved` events.
- `SourceImpact` records map to typed `ImpactRecorded` events with a bounded
  confidence value and explicit source/change/artifact keys.
- Source reference and payload hash are written as event markers for safe
  replay and duplicate detection.
- `AnalysisCurrent` is updated by the inline projection in the same Marten
  transaction. Async knowledge/impact graphs remain rebuildable projections;
  their daemon convergence is a separate M14 runtime gate.

## Evidence

`MartenProjectMigrationApplierTests.AppliesRepositoryAnalysisArtifactsAndImpactsOnTheAnalysisStreamIdempotently`
uses Testcontainers PostgreSQL and verifies:

1. Analysis, artifact, and impact records are mapped to typed events in
   deterministic aggregate order.
2. Aggregate reconstruction and the inline analysis view preserve repository,
   commit, artifact, impact, severity, and confidence data.
3. A second apply appends zero duplicate events and finds the original
   source-marker/hash values.
4. Artifact/impact records require an owning analysis source identity; malformed
   confidence or fact kinds fail closed in the mapper.

`Goal2MigrationPlannerTests` also verifies that repository facts use the owning
analysis stream and reject missing parent identity.

Migration suite result: `35 passed, 0 failed` on .NET 10, including the
Integrations operational and read-only Marten reconciliation checks.

## Remaining M13 obligations

Remaining integration writers, pre/post count and invariant reconciliation,
isolated backup/restore, projection rebuild, cutover, rollback, and production
self-host evidence remain `PROPOSED`.
