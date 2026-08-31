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

## Migration rules

| v0.1 data | vNext disposition |
| --- | --- |
| Project and lifecycle documents | `Projects` event stream + `ProjectCurrent` |
| Project roles/members/grants | `AccessControl` stream + inline permission view |
| Plan/requirements/milestones | `Planning` stream + plan projections |
| Tasks, versions, evidence | `TaskFlow` stream; preserve source/evidence keys |
| Analyzer runs, facts, impacts | `RepositoryIntelligence` streams + async graphs |
| GitHub credentials/delivery leases | operational documents; secrets stay external |

Every migrated record receives a deterministic identity and a source reference.
Rerunning the migration must upsert only the same stream/projection state; it
must never append a second business event for an already-migrated source key.

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
- Verify Wolverine durable inbox/outbox counts, retries, and dead letters;
  replay only after inspecting poison messages and preserving correlation IDs.
- Smoke test `/health`, authentication, project selection, task board, GitHub
  webhook signature rejection, and RabbitMQ delivery before declaring ready.
