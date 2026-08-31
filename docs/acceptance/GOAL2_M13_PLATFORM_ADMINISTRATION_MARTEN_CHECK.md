# GOAL2 M13 PlatformAdministration Marten apply check

Status: `CONFIRMED` for the typed model-level migration slice. This artifact
does not claim full M13 cutover, legacy deletion, backup/restore, rollback, or
production readiness.

## Scope

The migration tool handles the three system-scoped configuration records that
existed in the v0.1 management model:

- `GlobalAiProviderConfiguration` → `GlobalAiProvider` stream and inline
  `GlobalAiProviderCurrent` projection.
- `GlobalSystemSettings` → `GlobalSystemSettings` stream and inline
  `GlobalSystemSettingsCurrent` projection.
- `PlatformPolicy` → `PlatformPolicy` stream with `PlatformPolicyImported` and
  `PlatformPolicyCurrent` inline projection.

The mapper preserves updater/timestamp metadata, validates booleans, integer
limits, timestamps, and absolute support URLs, and keeps platform records out
of project streams. A repeated source reference finds the migration marker and
appends no duplicate event.

## Verification

```text
Goal2MigrationPlannerTests.PlansPlatformAdministrationRecordsAsSystemScopedEventStreams
MartenProjectMigrationApplierTests.AppliesPlatformAdministrationRecordsToSeparateTypedStreamsAndIsIdempotent
PlatformAdministrationVNext.Tests.PlatformPolicyTests
```

The .NET 10 build passes for the migration tool, API, and module tests. The
PostgreSQL Testcontainers test requires a host Docker socket and is exercised
in CI; the nested local Docker SDK invocation cannot reach the host-published
test database on this machine.

## Remaining M13 obligations

Full export pre/post counts, semantic invariant reconciliation, isolated
backup/restore, projection rebuild after restore, cutover, and rollback remain
required before changing the overall M13 status.
