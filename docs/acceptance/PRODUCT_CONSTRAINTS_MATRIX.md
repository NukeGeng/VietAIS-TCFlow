# Product Constraints Verification Matrix

This matrix separates historical v0.1 verification from GOAL2 target gates.
`CONFIRMED (v0.1)` means the referenced evidence proves the current released
behavior. `PROPOSED (GOAL2)` means the target requires new implementation and
must not be inferred from P14.

| Constraint | Status | Verification evidence |
| --- | --- | --- |
| 01. Initial setup is not overwhelming | CONFIRMED (v0.1); GOAL2 extension PROPOSED | Initial analysis auto-detects technologies, conventions, and authority defaults. GOAL2 must additionally validate domain/event/aggregate/projection suggestions. |
| 02. AI task noise is minimized | CONFIRMED (v0.1) | `MeaningfulChangeFilter`, incremental cosmetic-change tests, and P14 precision/false-positive/duplication metrics. |
| 03. Every impact is explainable | CONFIRMED (v0.1) | Knowledge graph impacts carry source change, affected artifact, reason, evidence, and confidence; fixture and task-trace integration tests verify the chain. |
| 04. Business context is repository-aware | CONFIRMED (v0.1); GOAL2 extension PROPOSED | Governance convention detection and targeted retrieval are verified; living planning/EventStorming/architecture context needs new acceptance evidence. |
| 05. Tasks do not become stale | CONFIRMED (v0.1); migration PROPOSED | Existing reconciliation covers create/update/merge/close/reopen/ignore; event-sourced TaskFlow parity and data migration remain unverified. |
| 06. AI does not silently modify tasks | CONFIRMED (v0.1); migration PROPOSED | Current versions/evidence/audit are verified; GOAL2 must prove equivalent event history, policy, and audit behavior. |
| 07. Product is not a weak Engineering Management clone | CONFIRMED (v0.1) for source-aware flow; GOAL2 extension PROPOSED | Repository graph/impact/source trace are verified; connected planning, EventStorming, living architecture, and data-model workflows are not yet implemented. |
| 08. Permissions remain understandable | CONFIRMED (v0.1); migration PROPOSED | Current backend/frontend 401/403/success behavior is verified; AccessControl projections and migration parity remain open. |
| 09. Realtime analysis is responsive | CONFIRMED (v0.1); GOAL2 baseline pending | P14 deterministic p95 is a regression threshold; daemon lag and durable-message latency need new measurements. |
| 10. AI verification is separate from human approval | CONFIRMED (v0.1); migration PROPOSED | Existing task verification proves separation; event-sourced TaskFlow must retain it. |
| 11. Self-host/LAN operation is maintainable | INFERRED / PROPOSED | Aspire startup, migrations, health checks, configuration validation, and repeatable README commands are confirmed. The versioned Docker Compose bundle, secret-safe environment template, health-gated services, backup/migration upgrade runbook, and CI startup smoke test are verified; production TLS, registry, and operator rollout remain external verification. |
| 12. AI quality is measurable | CONFIRMED (v0.1) | Executable P14 benchmark reports precision, recall, false-positive/negative rates, duplication, reconciliation accuracy, and fast-path p95. |
| 13. Product is not a thin LLM wrapper | CONFIRMED (v0.1) | Static Vue/ASP.NET/Marten analyzers, graph retrieval, governance, authority, reconciliation, and permission layers execute independently of Codex. |
| 14. Living Architecture is not static documentation | PROPOSED (GOAL2) | Requires source/decision mappings, drift detection, traceable architecture projections, and runtime acceptance evidence. |
| 15. Event Sourcing is purposeful | PROPOSED (GOAL2) | Per-feature persistence decisions, aggregate reconstruction, expected-version tests, and explicit operational-document exceptions are required. |
| 16. Projection consistency is explicit | PROPOSED (GOAL2) | Inline/async decisions, stale-state UX, daemon lag, convergence, idempotency, and rebuild tests are required. |
| 17. Durable messaging causes no duplicate effects | PROPOSED (GOAL2) | Wolverine inbox/outbox and RabbitMQ redelivery/retry/dead-letter tests are required. |
| 18. Bounded contexts remain isolated | PROPOSED (GOAL2) | Architecture tests must prevent direct cross-context infrastructure/persistence access. |
| 19. Migration preserves validated behavior | PROPOSED (GOAL2) | KEEP/PORT/REWRITE/REMOVE inventory, pre/post reconciliation, rollback, and v0.1 regression parity are required. |
| 20. Event-driven operations are observable | PROPOSED (GOAL2) | Stream, projection lag/rebuild, inbox/outbox, retry, and dead-letter telemetry must be verified. |
| Progressive trust | CONFIRMED (v0.1); migration PROPOSED | Current AI trust enforcement passes; vNext event/message handlers must preserve it. |
| User control | CONFIRMED (v0.1); migration PROPOSED | Existing AI audit/policy/reconciliation behavior passes; new planning/architecture automation must meet the same boundary. |
| Quality decision rule | CONFIRMED (v0.1); GOAL2 gate PROPOSED | P14 remains the old baseline; M14 must publish a distinct GOAL2 matrix. |
| Acceptable product behavior | CONFIRMED (v0.1); GOAL2 end-to-end PROPOSED | Current source change → evidence → impact → task flow is verified; the connected living-system and event-driven paths remain open. |
| Developer trust is the priority | CONFIRMED (v0.1); GOAL2 migration PROPOSED | Existing evidence boundaries are regression gates; replay, projections, messaging, and migration must add direct evidence. |

## Deployment-environment boundary

No v0.1 product-acceptance gate remains open. GOAL2 constraints 14–20 and all
rows explicitly marked proposed remain open until the new implementation and
M14 acceptance evidence exist. Production TLS, image-registry, backup/restore,
and operator rollout also remain environment-specific verification.

The managed-account reasoning worker and GitHub-originated flow are confirmed by
the non-secret runtime evidence in
[`P14_LIVE_CODEX_WORKER_2026-08-29.json`](evidence/P14_LIVE_CODEX_WORKER_2026-08-29.json)
and
[`P14_GITHUB_WEBHOOK_E2E_2026-08-30.json`](evidence/P14_GITHUB_WEBHOOK_E2E_2026-08-30.json).
