# VietAIS TCFlow GOAL2 Implementation Plan

## 1. Purpose and authority

This plan turns `GOAL2.md` into an ordered, verifiable migration program. It
preserves validated v0.1 behavior from `GOAL.md` while moving the product to
.NET 10, FullStackHero v10, bounded contexts, CQRS, Marten Event Sourcing,
inline and async projections, Wolverine, RabbitMQ, and .NET Aspire.

Authority order:

1. Explicit task acceptance criteria.
2. `GOAL2.md`.
3. Retained requirements in `GOAL.md`.
4. `PRODUCT_CONSTRAINTS.md`.
5. This plan.
6. `WORKFLOW.md`, `AGENTS.md`, and `GIT_RULES.md`.

If a higher-priority source conflicts with this plan, implementation stops
until the conflict is recorded and resolved.

## 2. State labels

- `CONFIRMED`: demonstrated by current source and direct verification.
- `INFERRED`: supported by evidence but not directly verified.
- `PROPOSED`: target behavior not yet implemented or verified.
- `BLOCKED`: cannot proceed without an external decision or dependency.

Target-state prose must not be reported as current runtime behavior.

## 3. Current baseline

### CONFIRMED — v0.1

- Release `v0.1.0` is based on .NET 9 and FullStackHero `2.0.4-rc`.
- The repository contains the ASP.NET Core API, Vue 3 application, Aspire 9
  host, PostgreSQL, Redis, Marten document persistence, deterministic source
  analyzers, GitHub App integration, reasoning/reconciliation, and P14 evidence.
- The current source targets `net9.0`; it does not contain Wolverine or
  RabbitMQ package references.
- Existing Repository Intelligence business state is primarily Marten
  document state. It is not proof of event-sourced aggregates or rebuildable
  read models.
- P14 proves the v0.1 source-aware workflow. It does not prove GOAL2 migration
  gates.

### PROPOSED — vNext

- A clean FullStackHero v10/.NET 10 baseline.
- Modular Monolith with the bounded contexts defined in GOAL2.
- DDD aggregates and vertical command/query slices.
- Marten Event Store for business history where event sourcing is valuable.
- Explicit inline and async projection ownership.
- Wolverine handlers, durable inbox/outbox, retry, and idempotency.
- RabbitMQ only for external/system integration messages.
- Replay/rebuild operations, projection lag observability, and migration
  reconciliation.

## 4. Target request and event flow

```text
Client / API
    ↓
Command / message
    ↓
Wolverine handler
    ↓
Application use case
    ↓
Aggregate / domain invariants
    ↓
Domain events
    ↓
Marten Event Store
    ├─ same transaction → inline critical read model
    └─ after commit → Marten async daemon → reporting/search/cross-stream model

Optional external integration event
    ↓
Wolverine outbox → RabbitMQ → other system
```

Marten async projections are not RabbitMQ consumers. RabbitMQ does not replace
the Marten async daemon.

## 5. Persistence decision rule

Every persistent model must be classified before implementation:

| Category | Use | Required checks |
| --- | --- | --- |
| Event Store | Aggregate history and business decisions | stream identity, expected version, event metadata, upcasting/version policy, invariant tests |
| Projection | Read/query models derived from events | inline/async choice, idempotency, rebuild test, lag/daemon observability |
| Operational Document | Credentials metadata, leases, delivery receipts, configuration, transient coordination | ownership, concurrency, retention, explicit save semantics |

Event sourcing is selected for business history, auditability, temporal
reasoning, or replay value. Simple operational data remains a document when an
event stream adds no product value.

## 6. Target repository ownership

```text
src/
  Host/
  BuildingBlocks/
    Domain/
    Application/
    Infrastructure/
    Web/
  Modules/
    PlatformAdministration/
    Projects/
    AccessControl/
    Planning/
    EventStorming/
    Architecture/
    TaskFlow/
    RepositoryIntelligence/
    Integrations/
  apps/vue/
tests/
  Unit/
  Integration/
  Architecture/
  Fixtures/
docs/
  architecture/
  migration/
  acceptance/
deploy/
```

The exact physical layout must follow the clean FullStackHero v10 baseline and
can be refined through an ADR. Bounded-context ownership and dependency
direction are mandatory even if folder names differ.

## 7. Cross-cutting quality gates

Every migration phase must prove, where applicable:

1. Aggregate Given/When/Then behavior and invalid decisions.
2. Expected-version optimistic concurrency.
3. Event metadata and backward-compatible contracts.
4. Inline projection visibility in the command transaction.
5. Async projection convergence, idempotency, replay, and rebuild.
6. Wolverine handler, inbox/outbox, retry, and duplicate-delivery behavior.
7. RabbitMQ routing and dead-letter behavior for external messages.
8. Permission plus resource/component scope, including 401/403/success paths.
9. Security-sensitive and AI mutations produce audit information.
10. OpenTelemetry/logging exposes stream identity, message correlation, daemon
    health, projection lag, retries, and dead letters without secrets.
11. Public contract producers and consumers are updated together.
12. Existing v0.1 behavior is reconciled before legacy code is removed.

## 8. Migration sequence

```text
M0 Documentation and inventory
    ↓
M1 Clean .NET 10 / FullStackHero v10 baseline
    ↓
M2 Eventing, CQRS, Marten, Wolverine, and observability building blocks
    ↓
M3 Projects
    ↓
M4 AccessControl
    ↓
M5 Planning
    ↓
M6 TaskFlow
    ↓
M7 EventStorming
    ↓
M8 Architecture
    ↓
M9 RepositoryIntelligence
    ↓
M10 Integrations and RabbitMQ
    ↓
M11 PlatformAdministration
    ↓
M12 Vue bounded-context workspace
    ↓
M13 Data migration, cutover, and self-host operations
    ↓
M14 GOAL2 end-to-end acceptance
```

Modules may be developed in parallel only after their upstream contracts and
building blocks are stable. Cross-context state changes use contracts/events,
not direct writes into another context's store.

## 9. Milestones

### M0 — Documentation and migration inventory

Owner: `ai` for cross-cutting architecture documentation.

Deliverables:

- Align all governing documents with GOAL2.
- Mark v0.1 evidence as historical and vNext behavior as proposed.
- Inventory current components as `KEEP`, `PORT`, `REWRITE`, or `REMOVE`.
- Record module ownership, dependency direction, public contracts, persistence
  categories, and migration risks.

Gate:

- Markdown structure and links validate.
- No document claims GOAL2 runtime completion.
- Migration matrix covers every current top-level component.

### M1 — Clean FullStackHero v10 / .NET 10 baseline

Owner: `backend`.

Status: `CONFIRMED` for the source baseline and the first `Projects` reference
slice. The complete GOAL2 migration is not complete; later milestones still
own the remaining bounded contexts and cutover.

Deliverables:

- Create a clean FullStackHero v10 baseline; do not blindly upgrade the
  existing package graph.
- Establish .NET 10 SDK pinning, central package management, Aspire, health,
  configuration validation, testing, and CI.
- Preserve the v0.1 branch/tag as rollback and behavioral reference.

Gate:

- Clean restore/build/test passes on .NET 10.
- The v10 AppHost builds with the migration API wired beside the historical
  API; direct API startup was verified against PostgreSQL with Marten schema
  setup and the async daemon running.
- Dependency inventory has no accidental duplicate framework abstraction.

### M2 — Shared event-driven building blocks

Owner: `backend`.

Status: `CONFIRMED` for the vNext reference slice. The bounded-context
migration remains incomplete until the later milestones and final GOAL2 matrix
are verified.

Deliverables:

- Domain event, aggregate, command/query, result, metadata, clock/identity,
  authorization, and validation conventions.
- Marten Event Store registration and inline/async daemon topology.
- Wolverine local handlers, durable inbox/outbox, retries, correlation, and
  idempotency.
- Projection administration and observability contracts.

Gate:

- Reference aggregate append/reload/concurrency tests pass.
- Reference inline and async projections rebuild from an empty projection
  store and converge to identical results.
- Duplicate durable messages cause no duplicate business effect.

Evidence: `docs/architecture/M2_EVENT_DRIVEN_BUILDING_BLOCKS.md` and the
PostgreSQL/Testcontainers coverage in
`src/vnext/Tests/EventSourcing.Tests`.

### M3 — Projects

Owner: `backend`.

Status: `CONFIRMED` for the vNext Projects lifecycle slice. Memberships,
project roles, authority/convention policy, and permission/resource scope are
owned by M4; the legacy module remains in place until M12 reconciliation.

Candidate aggregates/events:

- `Project` with create, rename, lifecycle, ownership-reference, and policy
  changes where business history is valuable.
- `ProjectCreated`, `ProjectRenamed`, `ProjectSuspended`, `ProjectActivated`.

Read models:

- Inline `ProjectCurrent` for immediate authorization and command feedback.
- Async portfolio/reporting/search views.

Gate:

- Atomic project creation behavior from v0.1 is retained.
- Existing project identities map deterministically to streams.
- Authorization, audit, replay, and concurrency tests pass.

Evidence: `docs/architecture/M3_PROJECTS_EVENT_MODEL.md`, the Projects unit
tests, and the PostgreSQL/Testcontainers event-store tests. Authorization
parity is explicitly deferred to M4 and is not claimed by this slice.

### M4 — AccessControl

Owner: `backend`.

Status: `CONFIRMED` for the vNext project-scoped role/membership slice. Full
FullStackHero Identity composition and migration of all legacy permission
surfaces remain part of the later cutover milestones.

Deliverables:

- Project membership, roles, grants, scopes, ownership transfer, AI policy,
  authority policy, and convention policy with system/project separation.
- Effective-permission projection with grant trace.

Gate:

- Project Owner cannot grant system permissions or manage another project.
- System Admin does not implicitly become Project Owner.
- Permission changes are auditable and effective reads meet the documented
  consistency requirement.

Evidence: `docs/architecture/M4_ACCESS_CONTROL_EVENT_MODEL.md` and
`src/vnext/Tests/AccessControl.Tests`. The current slice proves project/system
permission separation and owner invariants; token issuance and security-audit
read APIs are not claimed complete until PlatformAdministration/cutover.

### M5 — Planning

Owner: `backend`.

Status: `CONFIRMED` for the vNext Plan aggregate/reference slice. Permission
enforcement remains composed through AccessControl and the production host as
the remaining bounded contexts migrate.

Deliverables:

- Product intent, requirements, capabilities, roadmap, and planning decisions
  represented as living domain state with source/evidence links.
- Query models for current plan and roadmap views.

Gate:

- Planning history replays deterministically.
- Requirement/capability links do not cross context boundaries through direct
  persistence access.

Evidence: `docs/architecture/M5_PLANNING_EVENT_MODEL.md`, the Planning unit
tests, and the vNext host command/query endpoints.

### M6 — TaskFlow

Owner: `backend`.

Status: `CONFIRMED` for the vNext task lifecycle/reference slice. Reconciliation
of every legacy v0.1 task/version/evidence record remains a M13 cutover gate.

Deliverables:

- Event-sourced task lifecycle, assignment, review, source verification,
  reconciliation, AI verification, and human approval separation.
- Inline task-current view; async board/search/analytics views.

Gate:

- Invalid transitions fail without appending events.
- duplicate AI/source proposals reconcile rather than create duplicate tasks.
- Existing v0.1 tasks, versions, evidence, and audit reconcile to the new model.

### M7 — EventStorming

Owner: `backend`, with `frontend` consumer work.

Status: `CONFIRMED` for the vNext board/reference slice; traceability to every
legacy UI surface remains part of the M12 frontend cutover.

Deliverables:

- Boards, domains, commands, events, policies, aggregates, actors, hotspots,
  notes, links, and ordering.
- Traceability to planning, architecture, tasks, and source evidence.

Gate:

- Board changes preserve ordered history and concurrency semantics.
- Read models rebuild and linked identities remain stable.

### M8 — Architecture

Owner: `backend`, with `frontend` visualization work.

Status: `CONFIRMED` for the vNext living-architecture reference slice; source
mapping and visualization parity remain part of M9/M12 integration gates.

Deliverables:

- Bounded contexts, modules, dependencies, services, data models, ERDs,
  decisions, violations, and source mappings.
- Living architecture projections derived from user decisions and repository
  intelligence.

Gate:

- Architecture views distinguish confirmed source facts, inferred structure,
  and proposed design.
- Violations and drift are explainable and traceable.

### M9 — RepositoryIntelligence

Owner: `ai`, with backend adapters separated by contract.

Status: `CONFIRMED` for the vNext normalized-analysis reference slice; full
legacy analyzer precision/recall reconciliation remains an M13/M14 gate.

Deliverables:

- Port deterministic Vue/ASP.NET/Marten analysis, graph, contracts,
  conventions, impacts, reasoning, and reconciliation.
- Extend analyzers to recognize aggregates, domain events, projections,
  messages, and bounded-context dependencies.
- Async knowledge graph/search/analytics projections.

Gate:

- Static analysis remains before AI.
- v0.1 precision, recall, duplication, and latency baselines do not regress
  without recorded evidence and approval.
- Rebuild and incremental processing converge for equivalent source state.

### M10 — Integrations and RabbitMQ

Owner: `backend` for GitHub/provider adapters; `ai` for analyzer adapters.

Status: `CONFIRMED` for the normalized GitHub webhook and delivery boundary;
production broker topology and full provider migration remain an M13/M14
operational gate.

Deliverables:

- GitHub App OAuth/installation/webhook flows ported behind integration
  contracts.
- RabbitMQ external event topology using Wolverine outbox/inbox.
- delivery receipts, idempotency, retry, poison/dead-letter handling.

Gate:

- Invalid signatures fail closed.
- duplicate webhook and broker deliveries cause zero duplicate business
  effects.
- no token/private key/raw sensitive payload reaches events, messages, logs,
  audit, or projections.

### M11 — PlatformAdministration

Owner: `backend`.

Status: `CONFIRMED` for the vNext platform policy/provider metadata slice;
FullStackHero Identity authorization composition and audit query controls
remain M13/M14 cutover gates.

Deliverables:

- Platform policies, settings, AI provider availability, usage, tenant/user
  administration, audit query, projection status, replay/rebuild controls.

Gate:

- Administrative actions are permission-checked and audited.
- Projection rebuild controls cannot corrupt event history or expose secrets.

### M12 — Vue bounded-context workspace

Owner: `frontend`.

Deliverables:

- Reorganize routes, stores, clients, and views by product bounded context.
- Vietnamese remains the default UI language; English remains selectable.
- Surface consistency/loading/retry/forbidden/projection-lag states.

Gate:

- Typecheck, unit/component tests, lint, and production build pass.
- Frontend authorization remains UX only; backend is authoritative.
- Navigation updates immediately when project/session context changes.

### M13 — Data migration, cutover, and self-host operations

Owner: `backend`, with all domains verifying their data.

Status: `PROPOSED`; the migration/cutover runbook, acceptance matrix, a
versioned deterministic dry-run planner, a resumable operational ledger, and
typed `Projects`/`AccessControl`/`Planning`/`TaskFlow`/`RepositoryIntelligence`/
`EventStorming`/`Architecture` Marten apply slices plus the redacted
`Integrations` operational writer are published and tested.
Full-context pre/post reconciliation, isolated backup/restore, repeatable apply
coverage for all bounded contexts, and rollback evidence are still required
before this milestone can be marked confirmed.

Deliverables:

- Versioned migration from v0.1 documents to event streams, projections, or
  retained operational documents according to the migration matrix.
- `src/vnext/Tools/Goal2Migration` dry-run planner with schema validation,
  deterministic identities, source references, and duplicate-safe operations.
- Resumable migration ledger with payload-hash conflict detection and atomic
  checkpoint writes; this ledger is an operational document, not business
  event history.
- Typed `Projects` writer that maps `Project` and `ProjectState` records to
  deterministic event streams, preserves source-reference/payload-hash markers,
  and updates the inline `ProjectCurrent` projection in one Marten transaction.
- Typed `AccessControl` writer that maps project roles and memberships to the
  project-scoped access stream, preserves permission/resource/component scopes,
  and updates the inline effective-permission view in the same transaction.
- Typed `Planning` writer that maps plans and their requirements/milestones to
  deterministic plan streams and updates the inline plan view in the same
  transaction.
- Typed `TaskFlow` writer that maps legacy engineering-task snapshots to a
  deterministic task stream using `TaskProposed` plus a
  `TaskLifecycleReconciled` snapshot, preserves `TaskVersion` and
  `TaskEvidence` history/source keys without inventing transition history, and
  updates task read projections in the same transaction.
- Typed `RepositoryIntelligence` writer that maps analysis runs, source
  artifacts, and source impacts to deterministic analysis streams, preserves
  source/change/artifact keys and bounded confidence, and updates the inline
  analysis view in the same transaction.
- Typed `EventStorming` writer that maps boards, nodes, connections, hotspots,
  and ordering records to a deterministic board stream and updates the inline
  board canvas in the same transaction.
- Typed `Architecture` writer that maps models, modules, module relationships,
  entities, data relationships, and drift records to a deterministic model
  stream and updates the inline architecture view in the same transaction.
- Typed `Integrations` operational writer that stores only whitelisted GitHub
  installation/delivery metadata, rejects secret-bearing payloads, and applies
  source/hash idempotency without creating business events.
- Read-only Marten reconciliation command that verifies expected
  source-reference and payload-hash markers, reports missing/duplicate/mismatch
  conditions across event streams and Integrations operational documents, and
  fails closed without changing business state.
- Dry-run, reconciliation, rollback, backup/restore, replay/rebuild, and
  cutover runbooks.
- Self-host topology for PostgreSQL, Redis if retained, RabbitMQ, API, Vue,
  async daemon, and observability.

The Aspire AppHost now declares RabbitMQ as a persistent integration resource
and injects its endpoint/credentials into the vNext API. Marten async
projections remain configured on the local daemon; RabbitMQ is not used as an
internal projection transport. This is composition evidence only until an
isolated Aspire runtime transcript is captured.

The self-host bundle now provides a guarded `goal2` Compose profile with a
.NET 10 vNext API image, RabbitMQ, and an Nginx `/api/vnext/` canary route.
The default v0.1 services remain available for rollback until the canary passes
the full M13 runtime checks.

Gate:

- Pre/post counts and business invariants reconcile.
- Event-stream source markers, payload hashes, and retained Integrations
  operational documents reconcile without writes.
- Migration is repeatable and idempotent.
- Backup restore plus projection rebuild is demonstrated in an isolated stack.

### M14 — GOAL2 end-to-end acceptance

Owners: `backend`, `frontend`, and `ai` through separate branches and PRs.

Status: `PROPOSED`. See `docs/acceptance/GOAL2_M14_ACCEPTANCE_RECORD.md` for
the evidence already available and the runtime artifacts still required.

Required evidence:

- All GOAL2 quality gates and product constraints have direct tests or explicit
  runtime artifacts.
- Full stack starts through Aspire and self-host smoke tests.
- Event append, inline visibility, async convergence, replay/rebuild,
  concurrency, durable messaging, RabbitMQ failure handling, authorization,
  audit, GitHub ingestion, analyzer precision, task reconciliation, and UI
  workflows pass end to end.
- A new GOAL2 acceptance matrix is published without rewriting historical P14
  evidence.

## 10. Public contract strategy

Public contracts include HTTP APIs, commands, domain events, integration
events, messages, permission codes, configuration, projection schemas, and
analyzer fixtures. Breaking changes require producer, consumer, migration,
tests, and documentation to change together. Domain events are immutable
history; use additive evolution, versioned contracts, or explicit upcasters.

## 11. Program completion

GOAL2 is complete only when:

1. M0 through M14 pass their gates.
2. Every legacy component has a final migration classification and disposition.
3. All bounded contexts enforce dependency and persistence ownership.
4. Event streams reconstruct aggregates and all projections rebuild.
5. Durable messages are idempotent and operational failure paths are visible.
6. Backend, frontend, analyzer, architecture, integration, and end-to-end tests pass.
7. No relevant test is disabled or weakened.
8. No proposed or inferred behavior is reported as confirmed.

## 12. Milestone report

```text
Summary
Affected Areas
Verification
Contracts
Permissions
Persistence / Events
Projections
Messaging
Dependencies
Known Limitations
Acceptance Criteria Matrix
```
