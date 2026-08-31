# M3 Projects event model

Status: `CONFIRMED` for the vNext Projects slice in this branch. The legacy
v0.1 Project Management module remains the behavioral reference until M12
reconciliation and cutover.

## Ownership

The Projects bounded context owns the `ProjectAggregate` stream and its public
commands. A stream identity is the project `Guid`; callers may supply an
existing identifier during migration, so the same project maps to the same
Marten stream deterministically.

## Commands and decisions

| Command | Decision | Invalid decision |
| --- | --- | --- |
| `CreateProject` | Start one stream with `ProjectCreated` | Missing owner/name/correlation or invalid name |
| `RenameProject` | Append `ProjectRenamed` at the expected version | Suspended project, invalid name, stale version |
| `SuspendProject` | Append `ProjectSuspended` at the expected version | Already suspended or stale version |
| `ActivateProject` | Append `ProjectActivated` at the expected version | Already active or stale version |

The aggregate validates lifecycle invariants before an event is appended. A
failed decision leaves the session with no business event to commit.

## Read models

`ProjectCurrent` is an inline projection and is available in the same Marten
transaction for immediate project feedback and future authorization checks.
`ProjectPortfolioSummary` is an async projection owned by the Projects module;
it is intended for portfolio, reporting, and search views and may lag until the
Marten async daemon catches up. Both projections handle every Projects event
and can be rebuilt from the event stream.

## Traceability and concurrency

Lifecycle events carry actor, correlation, causation, project, source, and
occurrence metadata. Expected stream versions provide optimistic concurrency;
the losing writer receives a Marten version conflict and no second state is
published. Event metadata is the immutable business-operation trace for this
slice. Security/audit read APIs and project memberships remain owned by M4
AccessControl and the auditing module.

## Public endpoints

The vNext host exposes command endpoints for create, rename, suspend, and
activate, plus an immediate current-state query and an async portfolio summary
query. Endpoint authorization is intentionally completed with the M4
permission/resource-scope slice; the current M3 tests exercise domain and
event-store invariants rather than claiming frontend authorization is
authoritative.
