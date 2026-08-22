# TCFlow analyzers

This directory contains the technology-neutral source-analysis contracts and
the deterministic Vue analyzer introduced by project phase P5.

## Structure

- `core/` defines Artifact, Dependency, Evidence, Capability, Contract,
  Change, Impact, discovery, technology detection, stable identities, JSON
  serialization, and meaningful-change filtering.
- `vue/` extracts Vue and TypeScript facts without an LLM.
- `tests/` verifies deterministic output and evidence boundaries against the
  fixture in `samples/vue-full-application/`.

The Vue analyzer recognizes single-file components, `<script setup>`, props,
emits, form bindings and validation attributes, reactive/loading/error state,
API calls and payloads, response-field usage, TypeScript interfaces, Pinia
stores, Vue Router declarations, permission checks, filters, and pagination.

## Evidence policy

- Literal file extensions, declarations, fields, imports, methods, and static
  routes are `Confirmed`.
- Interpolated routes and business capabilities derived from source are
  `Inferred`.
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
