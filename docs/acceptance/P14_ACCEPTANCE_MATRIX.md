# P14 Acceptance Matrix

This matrix maps every criterion in `GOAL.md` sections 74–76 to direct
verification evidence. `CONFIRMED (branch)` means the automated evidence exists
and passes on the named feature branch or draft PR; it does not mean that the
protected integration branch has merged that PR. P14 remains incomplete until
the merge/runtime gates at the end of this document pass.

## Core product — GOAL section 74

| # | Criterion | Status | Direct evidence |
| ---: | --- | --- | --- |
| 1 | Detect technology | CONFIRMED (this branch) | `EndToEndQualityBenchmarkTests`; `CoreAnalyzerTests.TechnologyDetectionUsesDirectSourceSignals` |
| 2 | Detect Vue API calls | CONFIRMED (this branch) | `VueAnalyzerFixtureTests.FullApplicationFixtureMatchesExpectedGroundTruth`; P14 `artifact-api-call` fact |
| 3 | Detect request fields | CONFIRMED (this branch) | `VueAnalyzerFixtureTests`; P14 frontend request-field facts |
| 4 | Detect used response fields | CONFIRMED (ai) | `VueAnalyzerFixtureTests.FullApplicationFixtureMatchesExpectedGroundTruth` |
| 5 | Detect ASP.NET endpoints | CONFIRMED (this branch) | `AspNetAnalyzerFixtureTests.FullStackHeroAndTcFlowFixtureMatchesExpectedGroundTruth`; P14 endpoint fact |
| 6 | Detect request DTO | CONFIRMED (this branch) | `AspNetAnalyzerFixtureTests`; P14 request artifact/field facts |
| 7 | Detect response DTO | CONFIRMED (this branch) | `AspNetAnalyzerFixtureTests`; P14 response artifact/field facts |
| 8 | Detect Marten document | CONFIRMED (this branch) | `MartenAnalyzerFixtureTests.TcFlowFixtureMatchesDocumentsSessionsOperationsAndMissingSaveGroundTruth`; P14 document fact |
| 9 | Connect frontend call to backend endpoint | CONFIRMED (this branch) | `KnowledgeGraphFixtureTests.FullFixtureConnectsFrontendEndpointAndPersistenceWithoutUnrelatedContext`; P14 `edge-api-endpoint` fact |
| 10 | Connect backend endpoint to persistence | CONFIRMED (this branch) | `MartenAnalyzerFixtureTests.DependenciesConnectEndpointAndHandlersToDocuments`; P14 `edge-endpoint-document` fact |
| 11 | Detect contract mismatch | CONFIRMED (ai) | `ContractComparatorTests.CanonicalFixtureDetectsCategoryIdAndExplainableContractDifferences` |
| 12 | Apply authority rule | CONFIRMED (ai) | `GovernanceEngineTests.FrontendAndBackendAuthorityProduceDifferentExplainableImpacts` |
| 13 | Use repository convention | CONFIRMED (ai) | `GovernanceEngineTests.GeneratedPlansTargetExistingArtifactsAndDetectedNamingConventions` |
| 14 | Generate impact | CONFIRMED (ai) | `IncrementalMonitoringTests.ChangedPathIsReparsedWithoutDroppingUnchangedAnalyzerArtifactsAndQueuesTargetedReasoning` |
| 15 | Generate or update task | CONFIRMED (branches) | `ReasoningAndReconciliationTests.ReconciliationCoversCanonicalCreateUpdateMergeCloseReopenAndIgnoreCases`; PR #40 `Deep_reasoning_projects_a_source_aware_suggestion_with_trace_and_audit` |
| 16 | Trace task to source change | CONFIRMED (branches) | PR #40 `Deep_reasoning_projects_a_source_aware_suggestion_with_trace_and_audit`; `ReasoningAndReconciliationTests.MartenPersistsTaskVersionsAndAuditsWhileRejectingUnauthorizedClose` |
| 17 | Reconcile subsequent changes | CONFIRMED (branches) | P14 reconciliation metric; `IncrementalMonitoringTests.RevertIsDetectedAndDeepProcessorCreatesIgnoresThenClosesWithoutCallingAiForRevert`; PR #50 source-driven completion verification |
| 18 | Respect permission and component scope | CONFIRMED (backend branches) | PR #42 `ProjectAuthorizationIntegrationTests.Permission_engine_enforces_boundaries_traces_grants_and_audits_mutations`; `ProjectManagementIntegrationTests.Task_workflow_enforces_scope_preserves_trace_and_separates_ai_from_human_approval` |
| 19 | Audit user and AI actions | CONFIRMED (branches) | `ReasoningAndReconciliationTests.MartenPersistsSuggestedTaskWithSuggestOnlyPolicyAndSuggestionAudit`; PR #42 authorization/governance audit tests |

## Permission system — GOAL section 75

| # | Criterion | Status | Direct evidence |
| ---: | --- | --- | --- |
| 1 | System Admin and Project Owner are separate | CONFIRMED (PR #44) | `SystemAdministrationIntegrationTests.System_admin_inspects_and_suspends_projects_without_becoming_project_owner` |
| 2 | Owner manages only owned project | CONFIRMED (PR #42) | `ProjectAuthorizationIntegrationTests.Permission_engine_enforces_boundaries_traces_grants_and_audits_mutations` |
| 3 | Owner creates custom roles | CONFIRMED (PR #42) | `ProjectAdministrationIntegrationTests.Administration_reads_survive_reload_and_member_role_mutations_are_scoped_and_audited` |
| 4 | Owner grants only system-defined permissions | CONFIRMED (PR #42) | `ProjectAuthorizationIntegrationTests.Permission_engine_enforces_boundaries_traces_grants_and_audits_mutations` |
| 5 | User can have different roles per project | CONFIRMED (PR #42) | `ProjectAdministrationIntegrationTests.Administration_reads_survive_reload_and_member_role_mutations_are_scoped_and_audited` |
| 6 | Permission has resource/component scope | CONFIRMED (PR #42) | `ProjectAuthorizationIntegrationTests.Permission_engine_enforces_boundaries_traces_grants_and_audits_mutations` |
| 7 | Backend enforces permission | CONFIRMED (backend branches) | Authorization integration tests exercise 401/403/success paths |
| 8 | Frontend hides/disables by effective permission | CONFIRMED (frontend branches) | PR #43 `project-administration.spec.ts`; PR #47 `resource-lifecycle.spec.ts`; `App.spec.ts` |
| 9 | Forbidden direct API call returns 403 | CONFIRMED (backend branches) | `Project_role_endpoint_returns_401_then_403_then_success`; governance/resource lifecycle tests |
| 10 | Effective permission is traceable | CONFIRMED (PR #42) | `Permission_engine_enforces_boundaries_traces_grants_and_audits_mutations` |
| 11 | Role permission matrix works | CONFIRMED (PRs #42/#43) | persisted administration read test and `project-administration.spec.ts` |
| 12 | Role/permission changes are audited | CONFIRMED (PR #42) | authorization and administration mutation integration tests |
| 13 | AI has separate permission policy | CONFIRMED (ai/backend) | `ReasoningAndReconciliationTests.ProgressiveTrustRejectsUnauthorizedActionsAndLowConfidenceRemainsSuggestion`; governance API tests |
| 14 | Project Owner can transfer ownership | CONFIRMED (PR #42) | `ProjectAuthorizationIntegrationTests.Permission_engine_enforces_boundaries_traces_grants_and_audits_mutations` |
| 15 | System Admin manages platform resources | CONFIRMED (PRs #44/#45) | `SystemAdministrationIntegrationTests`; `system-administration.spec.ts` |

## AI architecture — GOAL section 76

| # | Criterion | Status | Direct evidence |
| ---: | --- | --- | --- |
| 1 | AI does not receive the whole repository | CONFIRMED (ai) | `ReasoningAndReconciliationTests.ReasoningReceivesOnlyTargetedGraphContextAndKeepsLowConfidenceProposed` |
| 2 | Static analyzer runs first | CONFIRMED (ai/backend) | `InitialRepositoryAnalysisTests`; PR #40 worker integration tests |
| 3 | Knowledge graph is used for retrieval | CONFIRMED (ai) | `KnowledgeGraphFixtureTests.FullFixtureConnectsFrontendEndpointAndPersistenceWithoutUnrelatedContext` |
| 4 | Context contains only related artifacts | CONFIRMED (this branch) | Knowledge graph retrieval test; P14 negative retrieval fact |
| 5 | AI output uses structured schema | CONFIRMED (ai) | `CodexProviderUsesManagedAccountAndStrictStructuredOutputWithoutCredentialContracts` |
| 6 | AI result carries confidence/evidence | CONFIRMED (ai) | `ReasoningReceivesOnlyTargetedGraphContextAndKeepsLowConfidenceProposed` |
| 7 | AI distinguishes CONFIRMED/INFERRED/PROPOSED | CONFIRMED (ai) | reasoning and contract comparator evidence-boundary tests |
| 8 | AI does not invent conflicting convention | CONFIRMED (ai) | `GovernanceEngineTests.GeneratedPlansTargetExistingArtifactsAndDetectedNamingConventions` |
| 9 | AI tasks respect authority | CONFIRMED (ai) | authority impact tests plus targeted reasoning test |
| 10 | AI actions respect AI permissions | CONFIRMED (ai) | progressive trust, suggest-only, and unauthorized-close tests |
| 11 | AI-generated tasks are audited | CONFIRMED (branches) | Marten suggestion audit test; PR #40 deep reasoning integration test |
| 12 | AI does not blindly duplicate tasks | CONFIRMED (this branch) | P14 duplication rate 0%; canonical reconciliation tests |

## P14 quality metrics

`P14_BENCHMARK_REPORT.md` records all seven required metrics. The thresholds and
27 labeled facts are executable source-controlled inputs rather than prose-only
claims.

## Integration and runtime gates

| Gate | Status | Evidence or remaining work |
| --- | --- | --- |
| Analyzer solution build/test | CONFIRMED (this branch) | Must remain green after final diff review |
| Backend integration suite | CONFIRMED (feature branches) | PRs #40, #42, #44, and #46 report passing suites; rerun on integrated branch required |
| Frontend typecheck/test/lint/build | CONFIRMED (feature branches) | PRs #41, #43, #45, and #47 report passing suites; rerun on integrated branch required |
| Aspire starts API, PostgreSQL, Redis, and Vue | CONFIRMED (local runtime) | Live stack health endpoint returned healthy on 2026-08-23 |
| Private GitHub repository installation and initial scan | CONFIRMED (local runtime) | `NukeGeng/Portfolio` source revision was fetched; unsupported Next.js was correctly reported without invented facts |
| Source-driven task completion verification | CONFIRMED (PR #50) | Deterministic evaluator passes locally; Marten integration coverage verifies AI policy, failed/inconclusive/passed outcomes, idempotency, evidence, versions, audit, and separation from human approval; rerun with Testcontainers after integration |
| Codex managed-account App Server handshake | CONFIRMED (local runtime) | `codex login status` reports ChatGPT authentication and `ConfiguredCodexAppServerProcessCompletesManagedAccountHandshake` passes against the installed CLI |
| Supported Vue + ASP.NET + Marten GitHub repository end-to-end | PROPOSED | Requires an installed supported repository and a push producing a meaningful contract change |
| Live Codex structured reasoning turn | CONFIRMED (local runtime) | `ConfiguredCodexAppServerCompletesStructuredReasoningTurn` passed against Codex CLI 0.144.2 using the ChatGPT-managed account, strict JSON output, `:read-only` permissions, an isolated runtime root, and an empty evidence/artifact context; the test requires both `TCFLOW_CODEX_EXECUTABLE` and `TCFLOW_RUN_LIVE_CODEX=true`, so ordinary CI remains offline |
| Protected-branch integration | PROPOSED | Draft PRs #39–#50 require user review/authorization before merge |

Overall P14 status: `PROPOSED / NOT YET COMPLETE`. Deterministic criteria and
branch-level evidence are present, but the supported live-repository
end-to-end run and protected-branch integration gates remain open.
