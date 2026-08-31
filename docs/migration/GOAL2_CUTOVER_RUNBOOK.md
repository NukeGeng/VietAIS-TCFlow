# GOAL2 migration, cutover, and restore runbook

Status: `PROPOSED` until executed against an isolated PostgreSQL/RabbitMQ
stack. This runbook is deliberately additive: v0.1 remains the rollback
reference until the post-cutover acceptance matrix is signed off.

## Preflight

1. Freeze schema-changing writes and record the v0.1 release/tag and current
   database backup identifier.
2. Validate `ConnectionStrings:marten`, the GitHub App secret configuration,
   RabbitMQ credentials, and JWT/bootstrap secrets through the secret manager.
   Never print values in CI logs.
3. Run the vNext restore/build/test gates and record commit SHA, event counts,
   projection positions, and analyzer precision/recall baselines.
4. Restore the backup into an isolated database and run the migration in dry
   mode. A dry run must be repeatable without changing source data.

The versioned dry-run planner is `src/vnext/Tools/Goal2Migration`. It accepts a
schema-versioned JSON export and writes a deterministic operation plan; it does
not connect to PostgreSQL, append events, or print payloads/secrets. Run it
against a redacted export before any apply step:

```bash
dotnet run --project src/vnext/Tools/Goal2Migration/Goal2Migration.csproj -- \
  --input /path/to/v0.1-export.v1.json \
  --output /path/to/goal2-migration-plan.json \
  --applied /path/to/already-applied-source-references.json
```

`--applied` is optional. The planner fails closed for unsupported schema
versions or unknown record kinds, assigns a deterministic target identity, and
marks repeated source references as `Skip` (`already-applied` or
`duplicate-in-export`). The output plan is an input to the later, isolated
Marten apply/reconciliation step; it is not evidence that production data has
already been migrated.

The tool also supports a resumable **operational migration ledger**. This is a
cutover checkpoint, not a replacement for the bounded-context Marten writers:

```bash
dotnet run --project src/vnext/Tools/Goal2Migration/Goal2Migration.csproj -- \
  --input /path/to/v0.1-export.v1.json \
  --output /path/to/goal2-migration-apply-report.json \
  --ledger /path/to/goal2-migration-ledger.v1.json \
  --apply
```

The first run records new source references and payload hashes. Repeating the
same command produces only skips; a changed hash for an existing source
reference fails closed. The ledger contains no source payloads or credentials.
The command still does **not** append business events; model-level writers must
consume the validated plan only after their field mapping and reconciliation
checks are approved.

The first approved model-level writers are the Projects, AccessControl,
Planning, TaskFlow, RepositoryIntelligence, EventStorming, and Architecture
slices. The Integrations writer retains only whitelisted GitHub installation
and delivery metadata as operational documents; it refuses secret-bearing
payload properties. In an isolated
PostgreSQL database, use `--apply-marten` with the same ledger to append typed
project/access/planning/task/analysis/board/architecture events and update the inline
`ProjectCurrent`/effective-permission/plan/task/analysis/board/architecture
projections in the Marten transaction:

```bash
dotnet run --project src/vnext/Tools/Goal2Migration/Goal2Migration.csproj -- \
  --input /path/to/projects-export.v1.json \
  --output /path/to/projects-marten-report.json \
  --ledger /path/to/projects-migration-ledger.v1.json \
  --apply-marten \
  --connection "$TCFLOW_MARTEN_CONNECTION"
```

For `EngineeringTask`, the writer appends `TaskProposed` followed by
`TaskLifecycleReconciled`; it preserves the imported snapshot without fabricating
accept/start/review transitions. `TaskVersion` and `TaskEvidence` records are
then appended to the owning task stream as typed immutable history.
`AnalysisRun`, `SourceArtifact`, and `SourceImpact` records are appended to the
owning analysis stream as typed repository facts. The writer
maps EventStorming board records to a board stream and Architecture records to
an architecture-model stream, preserving explicit parent and relationship
identities. Both streams update their inline read models in the same
transaction. GitHub credential/delivery records are stored separately in the
Integrations operational-document collection and are never serialized as
domain events; their operational write is committed separately after the
business-event transaction succeeds. It
fails closed for unsupported bounded-context records, missing
required project/access fields, unsupported lifecycle or permission values, an
existing stream without a migration marker, or a ledger marker that is not
present in Marten. Repeating the command reads the marker/hash from the event
stream and appends zero duplicate business events. The command is still an
isolated model slice: the full M13 pre/post reconciliation, backup/restore, and
rollback gate remains open.

After an apply, run the read-only Marten reconciliation against the same
versioned plan and an initialized target Event Store:

```bash
dotnet run --project src/vnext/Tools/Goal2Migration/Goal2Migration.csproj -- \
  --input /path/to/projects-export.v1.json \
  --output /path/to/projects-marten-reconciliation.json \
  --reconcile-marten \
  --connection "$TCFLOW_MARTEN_CONNECTION"
```

`--reconcile-marten` reads only migration source-reference and payload-hash
markers from target event streams, plus source-reference, kind, and payload-hash
fields from the typed Integrations operational-document collection. It reports
missing streams/documents, missing or duplicate markers, and hash mismatches;
it does not provision schema, append events, update projections, or modify
operational documents. Exit code `0` means all supported event-stream and
operational-document markers reconcile; exit code `3` means a mismatch was
found. Unsupported operational kinds fail closed.

## Migration rules

| v0.1 data | vNext disposition |
| --- | --- |
| Project and lifecycle documents | `Projects` event stream + `ProjectCurrent` |
| Project roles/members/grants | `AccessControl` stream + inline permission view |
| Plan/requirements/milestones | `Planning` stream + plan projections |
| Tasks, versions, evidence | `TaskFlow` stream; preserve source/evidence keys |
| Analyzer runs, facts, impacts | `RepositoryIntelligence` streams + async graphs |
| Event Storming boards, nodes, links, hotspots, ordering | `EventStorming` board stream + inline board canvas |
| Architecture models, modules, entities, relationships, drift | `Architecture` model stream + inline architecture view |
| GitHub credentials/delivery leases | operational documents; secrets stay external |

Every migrated record receives a deterministic identity and a source reference.
Rerunning the migration must upsert only the same stream/projection state; it
must never append a second business event for an already-migrated source key.

The planner's v1 source-reference format is `v0.1:{Kind}:{SourceId}`. The
operation's `TargetStream`, `TargetEventType`, and `Disposition` must be
reviewed against the model-level inventory before an apply implementation is
approved.

## Cutover and rollback

1. Enable dual-read comparison and route a canary project to vNext.
2. Compare counts, lifecycle invariants, permission outcomes, task identities,
   and analyzer baselines. Stop on any mismatch.
3. Switch reads, then writes, after the canary is accepted. Keep v0.1 read-only
   for the rollback window.
4. On rollback, disable vNext writes, restore the last known-good backup only
   in a new database, and point the v0.1 API at that restored copy. Do not
   delete the vNext event store; preserve it for diagnosis.

## Backup, replay, and operations

- Test PostgreSQL backup restore in an isolated stack before production.
- Rebuild inline/async projections from the event store and record the daemon
  position/lag before reopening traffic.
- In Aspire, provision the persistent RabbitMQ integration resource and inject
  its credentials only through parameters. Keep Marten Async Daemon processing
  on PostgreSQL; RabbitMQ is reserved for integration events.
- Verify Wolverine durable inbox/outbox counts, retries, and dead letters;
  replay only after inspecting poison messages and preserving correlation IDs.
- Smoke test `/health`, authentication, project selection, task board, GitHub
  webhook signature rejection, and RabbitMQ delivery before declaring ready.
