# GOAL2 M13 AccessControl Marten apply check

Status: `CONFIRMED` for the AccessControl model-level migration slice only.
This artifact does not claim full M13 cutover, legacy deletion, or M14
end-to-end readiness.

## Scope

- `ProjectRole` records map to `ProjectAccessInitialized` for the system Owner
  role, or `ProjectRoleCreated` plus `ProjectRolePermissionsUpdated` for a
  custom role.
- `ProjectMembership` records map to typed member events while retaining role
  identities and active/inactive state.
- Role/member records share the deterministic project access stream identity.
- Source reference and payload hash are written as event markers for safe
  replay and duplicate detection.
- `ProjectAccessCurrent` is updated by the inline projection in the same
  Marten transaction.

## Evidence

`MartenProjectMigrationApplierTests.AppliesAccessControlRolesAndMembershipsWithTypedEventsAndIsIdempotent`
uses Testcontainers PostgreSQL and verifies:

1. Owner initialization, custom role/permission events, and member events are
   typed and ordered by aggregate prerequisites.
2. Permission/resource scope is preserved and an effective permission query
   succeeds only for the matching repository.
3. Aggregate reconstruction and inline role/member view contain both members.
4. A second apply appends zero duplicate events and finds the original markers.
5. Missing/ambiguous role or permission data fails closed before a write.

Migration test result: `30 passed, 0 failed` on .NET 10 (including the
planner, Projects, AccessControl, Planning, TaskFlow, and RepositoryIntelligence
migration checks).

## Remaining M13 obligations

Remaining integration writers, pre/post count and invariant reconciliation,
isolated backup/restore, projection rebuild, cutover, rollback, and production
self-host evidence remain `PROPOSED`.
