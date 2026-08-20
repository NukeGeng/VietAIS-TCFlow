# TCFlow analyzers

This directory contains the technology-neutral source-analysis contracts and
the deterministic source analyzers and contract comparison introduced by
project phases P5-P8.

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
- `tests/` verifies deterministic output and evidence boundaries against the
  fixtures in `samples/vue-full-application/`,
  `samples/aspnet-full-application/`, and
  `samples/marten-full-application/`; contract comparison ground truth lives
  in `samples/contract-comparison/`.

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
analyzer producer, removes dangling derived records, and recomputes contract
edges. Retrieval walks dependencies in both directions to a bounded depth and
returns only the selected artifacts and their capabilities, contracts,
changes, impacts, mismatches, and evidence provenance.

Marten storage uses repository-indexed documents for every record category and
an optimistic-concurrency manifest. Writes use `IDocumentSession` and one
explicit `SaveChangesAsync`; reads use `IQuerySession`. P9 introduces no event
sourcing and no standalone graph database.

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
or permission signal.

## Build and test

```bash
dotnet restore src/analyzers/VietAIS.TCFlow.Analyzers.sln
dotnet build src/analyzers/VietAIS.TCFlow.Analyzers.sln --no-restore
dotnet test src/analyzers/VietAIS.TCFlow.Analyzers.sln --no-build --no-restore
```

Knowledge persistence tests require Docker because they start a disposable
PostgreSQL 16 container and verify a real Marten write/read/reconciliation
round trip.
