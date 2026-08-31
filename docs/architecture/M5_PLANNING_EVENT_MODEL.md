# M5 Planning event model

Status: `CONFIRMED` for the vNext reference slice. Planning is a separate
bounded context and links to a project by identifier; it does not write into
Projects or AccessControl persistence.

## Domain history

`PlanAggregate` owns product intent and roadmap structure. `PlanCreated` starts
the stream; `RequirementAdded` and `MilestoneAdded` append planning decisions
at an expected stream version. Text and identity invariants are validated
before appending an event, and duplicate requirement/milestone identities are
rejected during replayed aggregate decisions.

## Projections

`PlanCurrent` is inline and supplies the immediate plan/roadmap query. The
`PlanningOverview` async projection contains compact counts for dashboards and
reporting. Both projections are derived from the plan stream and can be
replayed/rebuilt; async visibility is intentionally allowed to lag.

## Cross-context and authorization boundary

The plan stores only `ProjectId` as a reference. AccessControl permission
integration is a cross-context application concern and is not implemented by a
direct dependency on another module's internals. The host composes module
endpoints and will apply project permission/resource scope as the AccessControl
pipeline is promoted to the production host.
