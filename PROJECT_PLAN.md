# PROJECT_PLAN.md

# VietAIS TCFlow Implementation Plan

## 1. Purpose

This plan turns `GOAL.md` into an ordered, verifiable implementation program.

It is governed by:

1. `GOAL.md`
2. `PRODUCT_CONSTRAINTS.md`
3. `WORKFLOW.md`
4. `AGENTS.md`
5. `GIT_RULES.md`

The plan does not replace those documents. If this plan conflicts with a
higher-priority source, the higher-priority source wins and this plan must be
updated.

## 2. Current Baseline

### CONFIRMED

- The repository contains the implemented backend, analyzers, Vue frontend,
  Aspire host, acceptance fixtures, and verification documentation; the two
  remaining P14 gates are explicitly external runtime gates.
- The remote has the required long-lived branches: `main`, `frontend`,
  `backend`, `mobile`, and `ai`.
- The required backend baseline is FullStackHero dotnet-starter-kit
  `2.0.4-rc`.
- FullStackHero `2.0.4-rc` targets .NET 9.
- A compatible .NET 9 SDK is available locally (`dotnet --version` reports
  `9.0.120`). The verified frontend toolchain is Node.js `24.19.0` with npm
  `11.17.0` (pinned in `.nvmrc` and used by CI); Docker is required for
  persistence integration tests and Aspire.
- Vue 3 + TypeScript + Vite is the product frontend stack.
- Marten + PostgreSQL is the persistence stack for new business modules.
- .NET Aspire is the local orchestration stack.

- The FullStackHero Blazor client is reference infrastructure, not the product
  frontend, because `GOAL.md` explicitly requires Vue 3.

- `RepositoryIntelligence` is the initial business module name, matching the
  example and terminology in `GOAL.md`.
- Upstream FullStackHero identity/infrastructure modules remain on their existing
  persistence mechanism while all new TCFlow business documents use Marten.

## 3. Target Repository Layout

The exact names must follow the imported FullStackHero baseline, but the target
ownership boundaries are:

```text
src/
  api/
    framework/                  FullStackHero framework infrastructure
    modules/
      RepositoryIntelligence/  TCFlow backend business module
    server/                     API composition root
  apps/
    vue/                        Vue 3 product frontend
  aspire/
    Host/                       Aspire orchestration
    service-defaults/           Shared Aspire defaults
  analyzers/
    core/                       Technology-neutral analysis contracts
    vue/                        Vue-specific analyzer
    aspnet/                     ASP.NET-specific analyzer
    marten/                     Marten-specific analyzer
  tests/
samples/
  vue-full-application/         Ground-truth analyzer fixture
```

No new architectural layer may be introduced merely to match this diagram.
Imported source conventions take precedence over illustrative folder names.

## 4. Domain and Branch Ownership

| Work area | Base branch | Feature branch examples |
|---|---|---|
| Vue product UI | `frontend` | `feat/frontend/project-shell` |
| ASP.NET/Marten business APIs | `backend` | `feat/backend/project-core` |
| Repository analyzers and reasoning | `ai` | `feat/ai/vue-analyzer` |
| Mobile | `mobile` | Deferred; not an initial product goal |
| Cross-cutting documentation | Owning domain | `docs/ai/project-governance` |

Cross-domain capabilities must be split into separate branches and connected
through explicit contract dependencies.

## 5. Delivery Principles

Every milestone must:

1. Define observable acceptance criteria before coding.
2. Prefer deterministic analysis over AI reasoning.
3. Preserve `CONFIRMED`, `INFERRED`, and `PROPOSED` evidence levels.
4. Keep authority and authorization independent.
5. Reconcile existing tasks before creating new tasks.
6. Audit security-sensitive and AI-driven changes.
7. Optimize for precision and explainability over task volume.
8. Build and test the affected domain.
9. Perform runtime verification when behavior crosses process or persistence
   boundaries.
10. Use a short-lived branch and Draft PR targeting the owning domain branch.

## 6. Phase and Dependency Map

```text
P0 Governance and Toolchain
  ↓
P1 FullStackHero + Aspire + PostgreSQL + Marten Foundation
  ↓
P2 Identity, Project Ownership, Permissions, Audit
  ↓
P3 Project, Repository, Task, Assignment, Review Core
  ├──────────────────────────────┐
  ↓                              ↓
P4 Vue Product Shell             P5 Analyzer Core + Vue Analyzer
  ↓                              ↓
P4b Administration UI            P6 ASP.NET Analyzer
                                 ↓
                              P7 Marten Analyzer
                                 ↓
                              P8 Contract Comparison
                                 ↓
                              P9 Knowledge Graph + Retrieval
                                 ↓
                              P10 Convention + Authority
                                 ↓
                              P11 AI/Codex + Reconciliation
                                 ↓
                              P12 GitHub Integration
                                 ↓
                              P13 Incremental Monitoring
                                 ↓
                              P14 End-to-End Acceptance and Benchmarks
```

## 7. Milestones

### P0 — Governance and Toolchain

Deliverables:

- Commit the governing documents through a documentation feature branch.
- Establish and protect all long-lived branches.
- Add `.gitignore`, editor settings, solution-level build entry points, and
  version pinning when the baseline source is imported.
- Make .NET 9 available without removing or modifying unrelated machine data.
- Record repeatable bootstrap commands in `README.md`.

Verification:

- All five long-lived branches exist remotely.
- No implementation commit is made directly on a protected branch.
- Required build tools report compatible versions.
- Governance Draft PR targets the correct domain branch.

### P1 — Backend Foundation

Owner: `backend`

Deliverables:

- Import FullStackHero `2.0.4-rc` as the backend foundation.
- Preserve its module, dependency injection, validation, authorization, error,
  logging, testing, and Aspire conventions.
- Add PostgreSQL and Marten registration for new TCFlow business modules.
- Add Aspire resources for API, PostgreSQL, required framework services, and
  the Vue frontend placeholder.
- Add health checks and configuration validation.

Acceptance evidence:

- Full solution builds with the pinned SDK.
- Aspire host starts required resources.
- API health endpoint reports healthy with PostgreSQL available.
- A Marten smoke document can be stored and loaded in an integration test.
- Existing FullStackHero infrastructure remains operational.

### P2 — Identity, Authorization, and Audit

Owner: `backend`

Deliverables:

- System Admin and Project Owner remain separate principals/scopes.
- Project, membership, system-defined permission definition, project role,
  role permission, member role, component scope, and resource scope models.
- Effective-permission calculation with grant trace.
- AI permission policy and progressive trust level.
- Ownership transfer with confirmation and audit.
- Audit records capturing actor, action, time, target, before, and after.

Required tests:

- Unauthenticated requests return `401`.
- Authenticated requests lacking permission/scope return `403`.
- Authorized requests succeed.
- Project Owner cannot manage another project or grant system permissions.
- System Admin does not implicitly become Project Owner.
- Role and permission mutations create audit entries.
- Effective permission output identifies source role and scope.

### P3 — Project Management Core

Owner: `backend`

Deliverables:

- Project creation initializes owner, default state, authority, convention,
  AI policy, and audit data atomically.
- Repository, component, feature, task, assignment, review, task evidence, and
  task version/history documents.
- Task lifecycle: Upcoming, In Progress, Ready for Review, Completed, Blocked,
  Rejected, and Cancelled.
- Pagination, filtering, and search using repository conventions.
- Source traceability from task to change, artifact, evidence, and impact.

Required tests:

- Marten write paths call `SaveChangesAsync`.
- Read paths use `IQuerySession`; write paths use `IDocumentSession`.
- Task transition permissions and invalid transitions are tested.
- AI verification and human approval are separate state.
- Task changes preserve version/history and audit records.

### P4 — Vue Product Frontend

Owner: `frontend`

Dependencies: P2 and P3 API contracts.

Deliverables:

- Vue 3 + TypeScript + Vite application using established project patterns.
- Login/session integration, dashboard, projects, repositories, analysis view,
  impact graph, features, task board, task detail/review.
- Project Administration and System Administration surfaces.
- Effective-permission-aware navigation/actions with useful missing-access
  explanations.
- Loading, empty, error, forbidden, and retry states.

Verification:

- Type checking, unit/component tests, and production build pass.
- Direct unauthorized API calls remain blocked by backend `403`.
- Frontend contracts match generated/verified backend contracts.
- Task board transitions reflect backend state after reload.

### P5 — Analyzer Core and Vue Analyzer

Owner: `ai`

Deliverables:

- Technology-neutral Artifact, Dependency, Evidence, Capability, Contract,
  Change, Impact, and analyzer contracts.
- File discovery and technology detection.
- Vue analyzer for components, script setup, props, emits, form fields, API
  calls, request bodies, response usage, TypeScript types, Pinia, Router,
  validation, loading/error state, permissions, filters, and pagination.
- Meaningful-change filter that ignores cosmetic-only changes.
- Ground-truth Vue fixture and expected artifact JSON.

Verification:

- Deterministic fixture tests prove paths, methods, fields, and evidence level.
- Cosmetic-only changes produce zero cross-layer impact/AI requests.
- Analyzer output never upgrades inferred UI intent to confirmed API evidence.

### P6 — ASP.NET Analyzer

Owner: `ai`

Deliverables:

- Endpoint, method, route, request, response, validation, authorization,
  handler/service dependency, and OpenAPI extraction.
- Fixtures based on the actual FullStackHero/TCFlow module convention.

Verification:

- Fixture output proves exact endpoint contracts and evidence locations.
- Literal deterministic facts are produced without AI.

### P7 — Marten Analyzer

Owner: `ai`

Deliverables:

- Detection of documents, `IQuerySession`, `IDocumentSession`, query, load,
  store, delete, and `SaveChangesAsync` calls.
- Dependency edges connecting endpoints/handlers to Marten documents.

Verification:

- Fixture tests cover read, write, delete, pagination, and missing-save cases.
- Event sourcing is not introduced.

### P8 — Contract Comparison

Owner: `ai`

Deliverables:

- Match Vue expected contracts to ASP.NET actual contracts.
- Compare method, route, request/response fields and types, optionality,
  validation, errors, pagination, and authorization.
- Emit explainable mismatch records with evidence and confidence.

Verification:

- The canonical `categoryId` mismatch is detected.
- Matching contracts produce no task noise.
- Ambiguous matches remain inferred/proposed.

### P9 — Repository Knowledge Graph and Retrieval

Owner: `ai`

Deliverables:

- Persist artifacts, dependencies, capabilities, contracts, changes, and
  impacts in Marten.
- Graph-neighborhood traversal for changed artifacts.
- Targeted retrieval context with explicit evidence provenance.

Verification:

- Frontend API call connects to backend endpoint and persistence artifact.
- Retrieval excludes unrelated repository content in fixture cases.
- Initial full scan and incremental graph update produce equivalent affected
  neighborhoods for the same source state.

### P10 — Convention and Authority Engine

Owner: `ai` with backend API support.

Deliverables:

- Detect and persist repository conventions.
- Project-configurable authority per knowledge type.
- Apply authority independently of actor permissions.
- Suggest defaults during onboarding without requiring full manual setup.

Verification:

- Frontend-authority and backend-authority mismatch cases lead to different,
  explainable impacts.
- Unauthorized authority mutation returns `403` and is audited when allowed.
- Generated plans follow detected module/naming conventions.

### P11 — AI/Codex Reasoning and Task Reconciliation

Owner: `ai`

Deliverables:

- Vendor-neutral `IAiReasoningProvider` abstraction.
- Codex/App Server-managed authentication; no cookie/token extraction.
- Structured impact/task schema with evidence and confidence.
- Progressive trust and AI action permission enforcement.
- Separate task generation and reconciliation services supporting Create,
  Update, Merge, Close, Reopen, and Ignore.
- Task version history and AI audit entries.

Verification:

- AI receives only targeted graph context.
- Low-confidence results remain suggestions.
- Existing related tasks are reconciled before creation.
- Reverts make obsolete tasks explainably close/cancel/re-evaluate.
- AI action without policy permission is rejected.

### P12 — GitHub Integration

Owner: `backend` plus `ai` analysis adapters.

Dependencies: local analyzer and reconciliation are stable.

Deliverables:

- Separate GitHub identity/repository access from Codex authentication.
- GitHub App installation and selected-repository access.
- Initial scan trigger and push/pull-request/merge event ingestion.
- Secure webhook validation and idempotency.

Verification:

- Invalid webhook signatures are rejected.
- Duplicate deliveries are idempotent.
- Installation scope cannot read an unselected repository.
- Repository access changes are audited.

### P13 — Incremental Monitoring

Owner: `ai`

Deliverables:

- Changed-file ingestion, incremental parse, graph update, fast deterministic
  path, queued deep reasoning path, impact generation, and reconciliation.
- Revert detection and re-analysis.

Measurable acceptance targets for the fixture repository:

- Cosmetic-only commit: no AI request and no cross-layer task.
- Incremental deterministic analysis p95: under 2 seconds for up to 20 changed
  files on the reference development machine.
- Duplicate webhook delivery: zero duplicate changes/tasks.
- Reconciliation fixture accuracy: 100% for the checked-in canonical cases.

Targets may be revised only with recorded benchmark evidence.

### P14 — End-to-End Acceptance and Quality Benchmarks

Owners: `backend`, `frontend`, and `ai` through separate branches/PRs.

The sample Vue + ASP.NET + Marten repository must prove all 19 core product
acceptance criteria in `GOAL.md` section 74, all 15 permission criteria in
section 75, and all 12 AI criteria in section 76.

Required benchmark reporting:

- Precision
- Recall
- False-positive rate
- False-negative rate
- Task duplication rate
- Task reconciliation accuracy
- Fast-path latency

Completion requires an acceptance matrix linking every criterion to an
automated test or an explicit runtime verification artifact.

## 8. Public Contract Strategy

- Backend contract is defined through the existing FullStackHero endpoint and
  OpenAPI conventions.
- Frontend API types must be generated from or mechanically checked against the
  backend contract when the baseline supports it.
- Analyzer fixture schemas are versioned public contracts.
- Permission codes use `resource.action` and are system-defined.
- Breaking changes require producer, consumer, tests, documentation, and
  analyzer fixtures to change together.

## 9. Persistence Strategy

- New TCFlow business persistence uses Marten documents.
- Reads use `IQuerySession`.
- Writes use `IDocumentSession` and explicitly call
  `SaveChangesAsync(cancellationToken)`.
- Existing FullStackHero infrastructure persistence remains intact unless a
  concrete requirement proves migration is necessary.
- Marten event sourcing and a standalone graph database are out of initial
  scope.

## 10. Security and Audit Gates

No milestone may pass if it:

- hard-codes role-name authorization;
- trusts frontend authorization;
- mixes system and project scope;
- lets Project Owners grant system permissions;
- stores or logs secrets, GitHub tokens, or Codex credentials;
- performs an administrative or AI mutation without required audit evidence;
- allows cross-project access outside effective resource/component scope.

## 11. Definition of Program Completion

The program is complete only when:

1. P0 through P14 are delivered through the required domain branch workflow.
2. All acceptance criteria from `GOAL.md` sections 74–76 have direct evidence.
3. All product constraints have an automated or documented verification.
4. Backend, frontend, analyzer, integration, and end-to-end suites pass.
5. Aspire runtime verification proves the local system starts as one stack.
6. The final diff and dependency inventory contain no unexplained changes.
7. No relevant test is disabled or weakened.
8. No known blocker is represented as completed.

## 12. Milestone Reporting Template

Each milestone report must contain:

```text
Summary
Affected Areas
Verification
Contracts
Permissions
Persistence
Dependencies
Known Limitations
Acceptance Criteria Matrix
```
