# P14 Acceptance Matrix

This matrix maps every criterion in `GOAL.md` sections 74–76 to direct
verification evidence. `CONFIRMED (main)` means the automated evidence exists
and passes on the protected integration branch. P14 remains incomplete only
until the explicitly external gates at the end of this document pass.

## Core product — GOAL section 74

| # | Criterion | Status | Direct evidence |
| ---: | --- | --- | --- |
| 1 | Detect technology | CONFIRMED (main) | `EndToEndQualityBenchmarkTests`; `CoreAnalyzerTests.TechnologyDetectionUsesDirectSourceSignals` |
| 2 | Detect Vue API calls | CONFIRMED (main) | `VueAnalyzerFixtureTests.FullApplicationFixtureMatchesExpectedGroundTruth`; P14 `artifact-api-call` fact |
| 3 | Detect request fields | CONFIRMED (main) | `VueAnalyzerFixtureTests`; P14 frontend request-field facts |
| 4 | Detect used response fields | CONFIRMED (main) | `VueAnalyzerFixtureTests.FullApplicationFixtureMatchesExpectedGroundTruth` |
| 5 | Detect ASP.NET endpoints | CONFIRMED (main) | `AspNetAnalyzerFixtureTests.FullStackHeroAndTcFlowFixtureMatchesExpectedGroundTruth`; P14 endpoint fact |
| 6 | Detect request DTO | CONFIRMED (main) | `AspNetAnalyzerFixtureTests`; P14 request artifact/field facts |
| 7 | Detect response DTO | CONFIRMED (main) | `AspNetAnalyzerFixtureTests`; P14 response artifact/field facts |
| 8 | Detect Marten document | CONFIRMED (main) | `MartenAnalyzerFixtureTests.TcFlowFixtureMatchesDocumentsSessionsOperationsAndMissingSaveGroundTruth`; P14 document fact |
| 9 | Connect frontend call to backend endpoint | CONFIRMED (main) | `KnowledgeGraphFixtureTests.FullFixtureConnectsFrontendEndpointAndPersistenceWithoutUnrelatedContext`; P14 `edge-api-endpoint` fact |
| 10 | Connect backend endpoint to persistence | CONFIRMED (main) | `MartenAnalyzerFixtureTests.DependenciesConnectEndpointAndHandlersToDocuments`; P14 `edge-endpoint-document` fact |
| 11 | Detect contract mismatch | CONFIRMED (main) | `ContractComparatorTests.CanonicalFixtureDetectsCategoryIdAndExplainableContractDifferences` |
| 12 | Apply authority rule | CONFIRMED (main) | `GovernanceEngineTests.FrontendAndBackendAuthorityProduceDifferentExplainableImpacts` |
| 13 | Use repository convention | CONFIRMED (main) | `GovernanceEngineTests.GeneratedPlansTargetExistingArtifactsAndDetectedNamingConventions` |
| 14 | Generate impact | CONFIRMED (main) | `IncrementalMonitoringTests.ChangedPathIsReparsedWithoutDroppingUnchangedAnalyzerArtifactsAndQueuesTargetedReasoning` |
| 15 | Generate or update task | CONFIRMED (main) | `ReasoningAndReconciliationTests.ReconciliationCoversCanonicalCreateUpdateMergeCloseReopenAndIgnoreCases`; deep-reasoning integration coverage |
| 16 | Trace task to source change | CONFIRMED (main) | deep-reasoning integration coverage; `ReasoningAndReconciliationTests.MartenPersistsTaskVersionsAndAuditsWhileRejectingUnauthorizedClose` |
| 16a | Verify source change resolves the task | CONFIRMED (main) | `GitHubIntegrationTests.Source_verification_requires_a_matched_contract_pair_before_passing`; `GitHubIntegrationTests.Source_verification_respects_ai_policy_and_keeps_human_approval_separate` |
| 17 | Reconcile subsequent changes | CONFIRMED (main) | P14 reconciliation metric; `IncrementalMonitoringTests.RevertIsDetectedAndDeepProcessorCreatesIgnoresThenClosesWithoutCallingAiForRevert`; source-driven completion verification |
| 18 | Respect permission and component scope | CONFIRMED (main) | `ProjectAuthorizationIntegrationTests.Permission_engine_enforces_boundaries_traces_grants_and_audits_mutations`; `ProjectManagementIntegrationTests.Task_workflow_enforces_scope_preserves_trace_and_separates_ai_from_human_approval` |
| 19 | Audit user and AI actions | CONFIRMED (main) | `ReasoningAndReconciliationTests.MartenPersistsSuggestedTaskWithSuggestOnlyPolicyAndSuggestionAudit`; authorization/governance audit tests |

## Permission system — GOAL section 75

| # | Criterion | Status | Direct evidence |
| ---: | --- | --- | --- |
| 1 | System Admin and Project Owner are separate | CONFIRMED (main) | `SystemAdministrationIntegrationTests.System_admin_inspects_and_suspends_projects_without_becoming_project_owner` |
| 2 | Owner manages only owned project | CONFIRMED (main) | `ProjectAuthorizationIntegrationTests.Permission_engine_enforces_boundaries_traces_grants_and_audits_mutations` |
| 3 | Owner creates custom roles | CONFIRMED (main) | `ProjectAdministrationIntegrationTests.Administration_reads_survive_reload_and_member_role_mutations_are_scoped_and_audited` |
| 4 | Owner grants only system-defined permissions | CONFIRMED (main) | `ProjectAuthorizationIntegrationTests.Permission_engine_enforces_boundaries_traces_grants_and_audits_mutations` |
| 5 | User can have different roles per project | CONFIRMED (main) | `ProjectAdministrationIntegrationTests.Administration_reads_survive_reload_and_member_role_mutations_are_scoped_and_audited` |
| 6 | Permission has resource/component scope | CONFIRMED (main) | `ProjectAuthorizationIntegrationTests.Permission_engine_enforces_boundaries_traces_grants_and_audits_mutations` |
| 7 | Backend enforces permission | CONFIRMED (main) | Authorization integration tests exercise 401/403/success paths |
| 8 | Frontend hides/disables by effective permission | CONFIRMED (main) | `project-administration.spec.ts`; `resource-lifecycle.spec.ts`; `App.spec.ts` |
| 9 | Forbidden direct API call returns 403 | CONFIRMED (main) | `Project_role_endpoint_returns_401_then_403_then_success`; governance/resource lifecycle tests |
| 10 | Effective permission is traceable | CONFIRMED (main) | `ProjectAuthorizationIntegrationTests.Permission_engine_enforces_boundaries_traces_grants_and_audits_mutations` |
| 11 | Role permission matrix works | CONFIRMED (main) | persisted administration read test and `project-administration.spec.ts` |
| 12 | Role/permission changes are audited | CONFIRMED (main) | authorization and administration mutation integration tests |
| 13 | AI has separate permission policy | CONFIRMED (main) | `ReasoningAndReconciliationTests.ProgressiveTrustRejectsUnauthorizedActionsAndLowConfidenceRemainsSuggestion`; governance API tests |
| 14 | Project Owner can transfer ownership | CONFIRMED (main) | `ProjectAuthorizationIntegrationTests.Permission_engine_enforces_boundaries_traces_grants_and_audits_mutations` |
| 15 | System Admin manages platform resources | CONFIRMED (main) | `SystemAdministrationIntegrationTests`; `system-administration.spec.ts` |

## AI architecture — GOAL section 76

| # | Criterion | Status | Direct evidence |
| ---: | --- | --- | --- |
| 1 | AI does not receive the whole repository | CONFIRMED (main) | `ReasoningAndReconciliationTests.ReasoningReceivesOnlyTargetedGraphContextAndKeepsLowConfidenceProposed` |
| 2 | Static analyzer runs first | CONFIRMED (main) | `InitialRepositoryAnalysisTests`; deep-reasoning worker integration tests |
| 3 | Knowledge graph is used for retrieval | CONFIRMED (main) | `KnowledgeGraphFixtureTests.FullFixtureConnectsFrontendEndpointAndPersistenceWithoutUnrelatedContext` |
| 4 | Context contains only related artifacts | CONFIRMED (main) | Knowledge graph retrieval test; P14 negative retrieval fact |
| 5 | AI output uses structured schema | CONFIRMED (main) | `CodexProviderUsesManagedAccountAndStrictStructuredOutputWithoutCredentialContracts` |
| 6 | AI result carries confidence/evidence | CONFIRMED (main) | `ReasoningReceivesOnlyTargetedGraphContextAndKeepsLowConfidenceProposed` |
| 7 | AI distinguishes CONFIRMED/INFERRED/PROPOSED | CONFIRMED (main) | reasoning and contract comparator evidence-boundary tests |
| 8 | AI does not invent conflicting convention | CONFIRMED (main) | `GovernanceEngineTests.GeneratedPlansTargetExistingArtifactsAndDetectedNamingConventions` |
| 9 | AI tasks respect authority | CONFIRMED (main) | authority impact tests plus targeted reasoning test |
| 10 | AI actions respect AI permissions | CONFIRMED (main) | progressive trust, suggest-only, and unauthorized-close tests |
| 11 | AI-generated tasks are audited | CONFIRMED (main) | Marten suggestion audit test; deep-reasoning integration test |
| 12 | AI does not blindly duplicate tasks | CONFIRMED (main) | P14 duplication rate 0%; canonical reconciliation tests |

## P14 quality metrics

`P14_BENCHMARK_REPORT.md` records all seven required metrics. The thresholds and
27 labeled facts are executable source-controlled inputs rather than prose-only
claims.

Product-level risk controls are mapped separately in
`PRODUCT_CONSTRAINTS_MATRIX.md`; each constraint is marked with direct source,
test, benchmark, or explicitly open external evidence.

## Integration and runtime gates

| Gate | Status | Evidence or remaining work |
| --- | --- | --- |
| Analyzer solution build/test | CONFIRMED (main) | .NET 9 build and all 59 analyzer tests pass locally and in required CI |
| Backend integration suite | CONFIRMED (main) | All 44 integration tests pass against isolated PostgreSQL containers locally and in required CI |
| Frontend typecheck/test/lint/build | CONFIRMED (main) | Typecheck, 28 Vitest tests, Oxlint, ESLint, Prettier, and production build pass with Node 24 locally and in required CI; reasoning status covers queued, processing, disabled worker/provider, and blocked-polling behavior |
| Aspire starts API, PostgreSQL, Redis, and Vue | CONFIRMED (local runtime) | On 2026-08-27 all five resources reported healthy; API health, Vue, and Swagger returned 200, while an unauthenticated project API call returned 401 |
| Private GitHub repository installation and initial scan | CONFIRMED (local runtime) | `NukeGeng/Portfolio` source revision was fetched; unsupported Next.js was correctly reported without invented facts |
| GitHub App installation configuration | CONFIRMED (GitHub readback) | App `vietais-tcflow` installation `155925244` selects `NukeGeng/Portfolio` and `NukeGeng/VietAIS-TCFlow`; installation permissions are read-only for contents/metadata/pull requests and no webhook events are subscribed |
| Source-driven task completion verification | CONFIRMED (main) | Deterministic and Marten integration coverage verifies AI policy, failed/inconclusive/passed outcomes, idempotency, evidence, versions, audit, and separation from human approval |
| Supported Vue + ASP.NET + Marten GitHub repository end-to-end | PROPOSED | Requires an installed supported repository and a push producing a meaningful contract change |
| Codex App Server managed-account handshake and structured turn | CONFIRMED (local provider) | Opt-in live reasoning test completed with the authenticated managed Codex executable on 2026-08-27 |
| Live Codex managed-account reasoning worker | PROPOSED | `RepositoryReasoning.Enabled` is false in development defaults; enable only with managed account and explicit AI policy |
| Long-lived branch protection | CONFIRMED (GitHub) | Active rulesets `main-owner-only-updates`, `protected-branches-pull-request`, and `protected-branches-safety` cover `main` and all long-lived branches; `main` updates are limited to the `NukeGeng` bypass actor through pull requests, all three quality gates are required, force-push/deletion are rejected, and collaborator readback shows only `NukeGeng` has admin/push access. |
| Protected-branch integration | CONFIRMED (main) | PRs #94 and #95 passed all required checks; PRs #96–#98 synchronized the domain branches, and PRs #99–#104 synchronized the acceptance readback. Current `main` is `e297e70822c62e45eb1bd3524e23f11071c2b48f`, and readback confirms all long-lived branch trees match it. |

Overall P14 status: `PROPOSED / NOT YET COMPLETE`. Deterministic criteria and
protected integration are present, but the supported live-repository reasoning
run and enabled live reasoning-worker gates remain open.
