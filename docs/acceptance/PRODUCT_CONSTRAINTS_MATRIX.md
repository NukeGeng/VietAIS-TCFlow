# Product Constraints Verification Matrix

This matrix records direct verification for every constraint in
`PRODUCT_CONSTRAINTS.md`. `CONFIRMED` means the referenced source or test was
run on the current branch. `PROPOSED` marks behavior that is intentionally
outside the current local-development acceptance boundary.

| Constraint | Status | Verification evidence |
| --- | --- | --- |
| 01. Initial setup is not overwhelming | CONFIRMED | Initial analysis auto-detects technologies, conventions, and authority defaults; `InitialRepositoryAnalysisTests` and onboarding APIs cover the flow. |
| 02. AI task noise is minimized | CONFIRMED | `MeaningfulChangeFilter`, incremental cosmetic-change tests, and P14 precision/false-positive/duplication metrics. |
| 03. Every impact is explainable | CONFIRMED | Knowledge graph impacts carry source change, affected artifact, reason, evidence, and confidence; fixture and task-trace integration tests verify the chain. |
| 04. Business context is repository-aware | CONFIRMED | Governance convention detection, authority mapping, targeted retrieval, and `GovernanceEngineTests`. |
| 05. Tasks do not become stale | CONFIRMED | `TaskReconciliationService` supports create/update/merge/close/reopen/ignore; revert and source-verification tests cover lifecycle changes. |
| 06. AI does not silently modify tasks | CONFIRMED | Marten `TaskVersion`, `TaskEvidence`, and audit records are written with task mutations; authorization and reasoning integration tests verify history. |
| 07. Product is not a weak Jira/Linear clone | CONFIRMED | Repository graph, impact, source trace, and analysis-status surfaces remain the primary workflow; task APIs consume source-backed traces. |
| 08. Permissions remain understandable | CONFIRMED | Backend returns permission/scope failures, frontend uses effective-permission state, and project/system authorization tests cover 401/403/success paths. |
| 09. Realtime analysis is responsive | CONFIRMED | Incremental fast path runs before deep reasoning; P14 benchmark records deterministic p95 below the 2-second target. |
| 10. AI verification is separate from human approval | CONFIRMED | `RepositoryTaskVerificationService` updates `AiVerificationStatus` while preserving `HumanApprovalStatus`; dedicated integration test asserts both states. |
| 11. Self-host/LAN operation is maintainable | INFERRED / PROPOSED | Aspire startup, migrations, health checks, configuration validation, and repeatable README commands are confirmed. The versioned Docker Compose bundle, secret-safe environment template, health-gated services, backup/migration upgrade runbook, and CI startup smoke test are verified; production TLS, registry, and operator rollout remain external verification. |
| 12. AI quality is measurable | CONFIRMED | Executable P14 benchmark reports precision, recall, false-positive/negative rates, duplication, reconciliation accuracy, and fast-path p95. |
| 13. Product is not a thin LLM wrapper | CONFIRMED | Static Vue/ASP.NET/Marten analyzers, graph retrieval, governance, authority, reconciliation, and permission layers execute independently of Codex. |
| Progressive trust | CONFIRMED | AI trust levels and permission policy are enforced by `AiActionAuthorizer`; negative-path reasoning and governance tests pass. |
| User control | CONFIRMED | AI actions are audited, policy-gated, reversible through reconciliation, and human approval remains distinct; authorization integration tests cover forbidden mutations. |
| Quality decision rule | CONFIRMED | P14 acceptance matrix, benchmark thresholds, diff review, and CI quality gates make precision/explainability/permission checks explicit before merge. |
| Acceptable product behavior | CONFIRMED (fixture) | End-to-end benchmark and source-aware task integration tests cover change → evidence → impact → task → verification → reconciliation. |
| Developer trust is the priority | CONFIRMED (fixture) | Evidence boundaries, no-phantom retrieval facts, zero duplicate rate, audit assertions, and explicit unresolved gates prevent unsupported claims. |

## Open external gates

- A supported live Vue + ASP.NET + Marten GitHub repository must produce a
  meaningful push through the installed GitHub App and webhook path; see the
  [`live acceptance checklist`](../../deploy/self-host/README.md#live-acceptance-checklist).

The full managed-account reasoning worker is confirmed locally. Its non-secret
runtime evidence is recorded in
[`P14_LIVE_CODEX_WORKER_2026-08-29.json`](evidence/P14_LIVE_CODEX_WORKER_2026-08-29.json).
