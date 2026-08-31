# M4 AccessControl event model

Status: `CONFIRMED` for the vNext reference slice. FullStackHero Identity
authentication and the legacy authorization surface remain migration inputs;
this slice establishes project-scoped authorization primitives without making a
project owner a system administrator.

## Boundary

AccessControl owns a deterministic access stream per project. The stream id is
derived from the project id with SHA-256, so it cannot collide with the
Projects aggregate stream while remaining stable during migration.

The owner is initialized with the project-scoped permission catalog only. No
system permission is present in that catalog, and role commands reject any
unknown/system permission code.

## Events and invariants

- `ProjectAccessInitialized` creates the Owner role and owner membership.
- `ProjectRoleCreated` creates a project role with no grants.
- `ProjectRolePermissionsUpdated` changes only non-system-defined roles and
  validates resource/component scope.
- `ProjectMemberAdded`, `ProjectMemberRolesAssigned`, and
  `ProjectMemberRemoved` manage active members; the owner cannot be removed or
  lose the Owner role.

Expected stream versions protect concurrent role/member changes. Every command
applies actor, correlation, causation, project, tenant, and source metadata
before Wolverine/Marten commits the event transaction.

## Projection and authorization

`ProjectAccessCurrent` is an inline projection used by the backend permission
evaluator. It includes role definitions, memberships, and grant trace inputs;
resource and component scope are evaluated server-side. Empty actor identity
is rejected as unauthenticated and a missing grant is rejected as forbidden.
Frontend checks are not trusted for authorization.

Authentication/token issuance is still supplied by the FullStackHero Identity
composition and is intentionally not duplicated in this module. Security audit
query/read APIs remain owned by the Auditing/PlatformAdministration milestones;
the immutable event metadata is the operation trace for this slice.
