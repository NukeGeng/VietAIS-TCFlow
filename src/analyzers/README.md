# TCFlow analyzers

This directory contains the technology-neutral source-analysis contracts and
the deterministic source analyzers, contract comparison, knowledge graph,
repository-governance engine, bounded AI reasoning/reconciliation engine,
GitHub analysis-request adapter, and incremental monitoring pipeline introduced
by project phases P5-P13.

## Structure

- `core/` defines Artifact, Dependency, Evidence, Capability, Contract,
  Change, Impact, discovery, technology detection, stable identities, JSON
  serialization, and meaningful-change filtering.
- `vue/` extracts Vue and TypeScript facts without an LLM.
- `aspnet/` extracts ASP.NET Core Minimal API/Carter contracts, OpenAPI
  metadata, validation, authorization, handlers, and injected dependencies.
- `marten/` extracts document schemas, query/write sessions, document
  operations, pagination, persistence commits, and missing-save diagnostics.
- `contracts/` pairs frontend expectations with backend contracts and emits
  explainable, evidence-linked mismatch records.
- `knowledge/` assembles analyzer outputs into a repository graph, performs
  bounded neighborhood retrieval, preserves record/evidence provenance, and
  persists repository-scoped graph records with Marten.
- `governance/` detects evidence-backed repository conventions, evaluates
  project authority policy, builds convention-aware impact plans, and persists
  convention profiles with Marten.
- `reasoning/` supplies the vendor-neutral AI provider boundary, targeted
  reasoning context, structured impact/task output, progressive AI trust,
  source-aware task reconciliation, task version history, and AI audit
  persistence.
- `github/` validates the backend's GitHub analysis-request contract and maps
  initial-scan, push, pull-request, and merge requests into provider-neutral
  repository analysis work items. It does not fetch source or call AI.
- `monitoring/` validates and analyzes bounded initial repository snapshots,
  then ingests changed-file contents, claims delivery correlation keys,
  filters cosmetic work, applies path-scoped analyzer updates, emits
  deterministic impacts, queues bounded deep-reasoning jobs, detects exact
  reverts, and reconciles source-backed tasks under AI policy.
- `tests/` verifies deterministic output and evidence boundaries against the
  fixtures in `samples/vue-full-application/`,
  `samples/aspnet-full-application/`, and
  `samples/marten-full-application/`; contract comparison ground truth lives
  in `samples/contract-comparison/`, and canonical reconciliation outcomes
  live in `samples/reasoning/`. The cross-domain GitHub request fixture lives
  in `samples/github/`; P13 latency, duplicate, cosmetic, and reconciliation
  targets live in `samples/incremental-monitoring/`.

P14 adds a cross-layer quality gate in
`tests/monitoring/EndToEndQualityBenchmarkTests.cs`. Its versioned ground truth
is in `samples/end-to-end-acceptance/`, and the complete criterion-to-evidence
matrix plus measured report are in `docs/acceptance/`. The benchmark reports
precision, recall, false-positive/false-negative rates, task duplication,
reconciliation accuracy, and deterministic fast-path p95 latency.

The Vue analyzer recognizes single-file components, `<script setup>`, props,
emits, form bindings and validation attributes, reactive/loading/error state,
API calls and payloads, response-field usage, TypeScript interfaces, Pinia
stores, Vue Router declarations, permission checks, filters, and pagination.

The ASP.NET analyzer follows the repository's FullStackHero and TCFlow
conventions. It composes the versioned Carter root, module and nested group
prefixes, and endpoint-local routes; extracts request/response DTO fields,
FluentValidation rules, permission requirements, authenticated groups,
MediatR handlers and constructor dependencies; and records operation name,
summary, description, response/error status, and API version as OpenAPI
evidence.

The Marten analyzer recognizes `IQuerySession` and `IDocumentSession`, schema
configuration, `Query`, `LoadAsync`, `Store`, `Delete`, and
`SaveChangesAsync`. It connects endpoint/handler activity to document
artifacts, records `Skip`/`Take` pagination, and emits `MARTEN001` when a write
scope has no persistence commit. It analyzes document storage only and does
not introduce Marten event sourcing.

The contract comparator checks HTTP method and normalized route, request and
response fields, JSON-compatible types, optionality, validation constraints,
documented errors, pagination, and authorization. Exact normalized routes are
confirmed when both inputs are confirmed. A unique suffix-compatible route is
inferred with capped confidence; equally plausible candidates remain inferred
and do not emit mismatch noise. Generic Vue error-state evidence is not treated
as a specific HTTP status contract.

The knowledge graph adds a deterministic API-call-to-endpoint edge from an
unambiguous contract pair and reuses analyzer dependencies to reach handlers,
DTOs, validators, and Marten documents. Incremental replacement is scoped by
analyzer producer or changed repository path, preserves unaffected analyzer
records, removes dangling derived records, and recomputes contract edges.
Retrieval walks dependencies in both directions to a bounded depth and returns
only the selected artifacts and their capabilities, contracts, changes,
impacts, mismatches, and evidence provenance.

Marten storage uses repository-indexed documents for every record category and
an optimistic-concurrency manifest. Writes use `IDocumentSession` and one
explicit `SaveChangesAsync`; reads use `IQuerySession`. P9 introduces no event
sourcing and no standalone graph database.

The governance engine detects architecture, API, persistence, validation,
naming, module-layout, state-management, and routing conventions from the
knowledge graph. Onboarding authority defaults remain `Proposed` until a
project configures them. Authority determines which source a mismatch should
align to; it is deliberately independent from actor permissions, which remain
the backend's responsibility. Plans only reference artifacts already present
in the graph and carry both convention and source-evidence identities.

Convention profiles use optimistic revision checks. Writes use
`IDocumentSession` followed by explicit `SaveChangesAsync`, while reads use
`IQuerySession`.

The reasoning stage receives a bounded `RetrievalContext` produced by the
knowledge graph instead of the complete repository. Model-produced artifact
and evidence identities are rejected unless they already exist in that
context. Deterministic facts cannot be promoted by the model: confirmed model
claims are capped at inferred, and confidence below `0.7` remains proposed.

`IAiReasoningProvider` keeps the reasoning engine vendor-neutral. The Codex
adapter uses the official [Codex App Server protocol](https://developers.openai.com/codex/app-server/)
over JSONL stdio. Authentication state is read from the Codex-managed account;
the adapter does not accept, extract, or persist cookies, API keys, or OAuth
tokens. Turns run in an isolated working directory with a restricted read-only
permission profile, an explicit runtime workspace root, and a strict JSON
output schema. Live structured-turn coverage is opt-in through
`TCFLOW_RUN_LIVE_CODEX=true`; ordinary builds and CI do not invoke the managed
account.

Task generation and reconciliation are separate. Reconciliation first finds
tasks by project, repository, and source-backed correlation key, then chooses
Create, Update, Merge, Close, Reopen, or Ignore. Marten writes the current task,
an immutable version snapshot, and an AI audit record in one explicit commit.
Every mutation is checked against both the configured AI permission and the
project's progressive trust ceiling.

The GitHub adapter accepts the backend's pending `RepositoryAnalysisRequest`
JSON, including its current numeric enum representation. It validates request,
project, repository, and delivery correlation; enforces full-scan versus
incremental event invariants; rejects unsafe repository-relative paths; and
maps GitHub file states into the analyzer core's technology-neutral
`RepositoryAnalysisWorkItem` and `ChangeKind`. Content retrieval is supplied
through the P13 change-source boundary.

The initial-analysis service accepts only initial full-scan work items. A
repository snapshot source supplies one immutable source revision and bounded,
safe repository-relative files. Repository-level applicability gates the Vue,
ASP.NET, and Marten analyzers before parsing, preventing a React/Next.js
TypeScript repository from being interpreted as Vue. Supported facts produce
a revisioned graph, detected conventions, and suggested authority defaults. If
no configured analyzer can produce source facts, the result is explicitly
`Unsupported` with diagnostic `ANALYSIS001`; the pipeline does not invent
tasks or confirmed facts for an unsupported stack.

The incremental fast path accepts only provider-neutral incremental work items
and validates loaded contents against event paths unless the GitHub contract
requires deferred pull-request file retrieval. A concurrent delivery claim
prevents the same correlation key from updating the graph or queue twice.
Cosmetic/non-behavioral batches stop before parsing. Meaningful batches run
only analyzers affected by the changed extensions and patch their records by
path, so unrelated artifacts from the same analyzer remain intact.

Fast-path results carry elapsed time, source changes, immediate impacts, graph
revision, and an optional deep-reasoning job. The job snapshots only mismatch
IDs in the targeted neighborhood; it never sends the full repository to AI.
Exact inverse before/after hashes identify reverts. Revert jobs skip fresh AI
reasoning and reconcile tasks already traced to the reverted source change.
The deep worker still separates AI verification from human approval and routes
every mutation through the existing trust, permission, version-history, and
audit writer.

## Evidence policy

- Literal file extensions, declarations, fields, imports, methods, and static
  routes are `Confirmed`.
- Interpolated routes and business capabilities derived from source are
  `Inferred`.
- ASP.NET endpoints whose Carter/group prefix cannot be resolved remain
  `Inferred` and emit `ASPNET001`; they are never promoted to confirmed.
- Contract pairs created from suffix-compatible routes remain `Inferred`, even
  when both source contracts are confirmed.
- Ambiguous contract candidates remain `Inferred` and emit no mismatch until a
  backend contract can be selected without guessing.
- The analyzer does not emit a confirmed API contract from form intent alone.
- `Proposed` is reserved for later planning stages and is never presented as
  repository truth.

Stable SHA-256-derived IDs and ordinal sorting make identical repositories
produce byte-for-byte identical JSON, regardless of discovery input order.

## Meaningful changes

Whitespace-only, documentation-only, stylesheet-only, and Vue `<style>`-only
changes have no cross-layer potential and recommend zero AI requests.
Executable changes are re-analyzed deterministically; one reconciliation pass
is recommended only when the changed text contains a contract, state, route,
permission, ASP.NET endpoint, or Marten persistence signal.

## Build and test

```bash
dotnet restore src/analyzers/VietAIS.TCFlow.Analyzers.sln
dotnet build src/analyzers/VietAIS.TCFlow.Analyzers.sln --no-restore
dotnet test src/analyzers/VietAIS.TCFlow.Analyzers.sln --no-build --no-restore
```

Knowledge and governance persistence tests require Docker because they start
disposable PostgreSQL 16 containers and verify real Marten persistence round
trips.
