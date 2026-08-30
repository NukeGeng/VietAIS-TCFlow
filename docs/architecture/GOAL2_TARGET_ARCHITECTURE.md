# GOAL2 Target Architecture

Status: `PROPOSED` until the corresponding `PROJECT_PLAN.md` gates pass.

This document is the concise architecture map for GOAL2. `GOAL2.md` remains
the authoritative specification.

## System shape

```text
Vue / external clients
        ↓ HTTP
ASP.NET Core host
        ↓
Wolverine command/query/message handlers
        ↓
Application use cases
        ↓
DDD aggregates and domain services
        ↓ domain events
Marten Event Store
   ├─ inline projections ──→ critical current-state queries
   └─ async daemon ────────→ search, reporting, analytics, cross-stream views

Committed domain event
        └─ optional integration translation
               ↓ Wolverine outbox
             RabbitMQ
               ↓
          external systems
```

## Architectural rules

1. Commands change state; queries read projections or operational documents.
2. Aggregates own invariants and emit past-tense domain events.
3. Expected stream versions protect concurrent business decisions.
4. Critical read models may be inline; expensive, cross-stream, search, and
   reporting models are async.
5. All projections must be idempotent and rebuildable from event history.
6. Marten's async daemon owns async projections. RabbitMQ is reserved for
   external/system integration and does not replace that daemon.
7. Wolverine owns handler dispatch and durable inbox/outbox behavior.
8. Cross-context communication uses public contracts or integration events,
   never direct writes to another context's persistence.
9. Domain-event history supports temporal reasoning but does not replace
   permission-checked security audit records.
10. Static source analysis remains authoritative for deterministic facts; AI
    handles semantic ambiguity and never silently upgrades evidence levels.

## Bounded contexts

| Context | Core responsibility | Likely event-sourced state | Primary projections |
| --- | --- | --- | --- |
| Projects | Project identity, lifecycle, ownership reference | Project decisions and lifecycle | ProjectCurrent inline; portfolio/search async |
| AccessControl | Membership, roles, scoped grants, policies | Membership/grant/policy changes where history matters | EffectivePermission inline; audit/reporting async |
| Planning | Product intent, requirements, capabilities, roadmaps | Planning decisions and revisions | Current plan inline; roadmap/coverage async |
| EventStorming | Boards, commands, events, policies, aggregates, hotspots | Board collaboration and ordering decisions | Board view inline; cross-board analysis async |
| Architecture | Contexts, modules, dependencies, ERD, decisions, drift | Architecture decisions and lifecycle | Current architecture inline; graph/violations async |
| TaskFlow | Task lifecycle, review, assignment, reconciliation | Task transitions and source-verification decisions | TaskCurrent inline; board/search/analytics async |
| RepositoryIntelligence | Source facts, contracts, graph, impacts, AI reasoning | Only decisions/history with replay value; deterministic bulk facts may remain documents/projections | Knowledge graph/search/analytics async |
| Integrations | GitHub/provider connections and external deliveries | Integration lifecycle decisions as needed | Installation/delivery operational views |
| PlatformAdministration | Platform settings, policies, providers, usage, operations | Sensitive configuration decisions where history matters | Current settings inline; usage/operations async |

The table is a decision starting point, not authorization to event-source every
record. Each feature must still justify its persistence category.

## Persistence categories

### Event Store

Use for business decisions needing history, replay, temporal reasoning, or
auditability. Events contain stable stream identity, schema/version, actor and
correlation metadata, occurrence time, and tenant/project scope where relevant.
They must not contain secrets.

### Projection Store

Use for query-optimized read models derived from events. The feature must state
whether consistency is immediate or eventual, expose a safe stale/loading
state where needed, and include rebuild verification.

### Operational Documents

Use for coordination or state whose history has little product value, such as
delivery receipts, leases, installation metadata, provider availability, and
some analyzer snapshots. Concurrency, retention, ownership, and explicit
commit semantics still apply.

## Dependency direction

```text
Endpoints / transport
        ↓
Application feature slice
        ↓
Domain

Infrastructure → implements ports owned by application/domain
Other bounded contexts → public contracts or integration events only
```

Domain projects must not depend on ASP.NET, Marten, Wolverine, RabbitMQ, or
another bounded context's infrastructure.

## Consistency selection

Choose inline when the same request or immediate authorization decision cannot
safely tolerate stale state. Choose async for expensive computation,
cross-stream aggregation, search, reporting, dashboards, and knowledge graphs.
Document the user-visible behavior while async data catches up.

## Operational requirements

- Health and readiness for PostgreSQL, Marten async daemon, Wolverine durable
  storage, RabbitMQ, and external providers.
- Metrics/traces for event append latency, concurrency conflicts, projection
  lag, rebuild progress, handler retry, inbox/outbox backlog, and dead letters.
- Correlation and causation identifiers across HTTP, event, projection, and
  message boundaries.
- Backup/restore of the Event Store before projection stores; projections must
  be disposable and rebuildable.
- Replay/rebuild controls must be permission-checked and auditable.

## Verification boundary

This architecture becomes `CONFIRMED` incrementally. Passing compilation alone
does not confirm it; the evidence gates are defined per milestone in
`PROJECT_PLAN.md` and culminate in a new GOAL2 acceptance matrix.
