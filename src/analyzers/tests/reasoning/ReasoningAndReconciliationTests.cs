using System.Text.Json;
using Marten;
using Testcontainers.PostgreSql;
using VietAIS.TCFlow.Analyzers.AspNet;
using VietAIS.TCFlow.Analyzers.Contracts;
using VietAIS.TCFlow.Analyzers.Core;
using VietAIS.TCFlow.Analyzers.Governance;
using VietAIS.TCFlow.Analyzers.Knowledge;
using VietAIS.TCFlow.Analyzers.Marten;
using VietAIS.TCFlow.Analyzers.Reasoning;
using VietAIS.TCFlow.Analyzers.Vue;
using Xunit;

namespace VietAIS.TCFlow.Analyzers.Reasoning.Tests;

public sealed class ReasoningAndReconciliationTests
{
    [Fact]
    public async Task ReasoningReceivesOnlyTargetedGraphContextAndKeepsLowConfidenceProposed()
    {
        var fixture = await BuildFixture();
        var provider = new CapturingReasoningProvider(context =>
        {
            var artifact = context.GraphContext.Artifacts[0];
            var evidence = context.GraphContext.Evidence[0];
            return new AiImpactReasoningResult(
                "The changed frontend contract may require a backend alignment.",
                ImpactSeverity.High,
                EvidenceLevel.Confirmed,
                0.6m,
                [evidence.Id],
                [
                    new AiTaskReasoningResult(
                        "Align create product request",
                        "Apply the authority decision using the detected request convention.",
                        PlanTargetComponent.Backend,
                        EvidenceLevel.Confirmed,
                        0.6m,
                        [artifact.Id],
                        [evidence.Id],
                        ["Handle categoryId consistently."])
                ]);
        });
        var policy = Policy(AiTrustLevel.SuggestOnly, AiPermissionCodes.AnalysisRun, AiPermissionCodes.TaskSuggest);

        var result = await new SourceAwareReasoningService(provider).AnalyzeAsync(
            ProjectId,
            fixture.Graph,
            [fixture.Change.Id],
            fixture.Authority,
            fixture.Conventions,
            policy,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(provider.Context);
        Assert.True(fixture.Graph.Artifacts.Count > provider.Context.GraphContext.Artifacts.Count);
        Assert.DoesNotContain(provider.Context.GraphContext.Artifacts,
            artifact => artifact.Path.Contains("UnrelatedSettings", StringComparison.Ordinal));
        Assert.All(provider.Context.GraphContext.Evidence, evidence => Assert.Contains(
            provider.Context.GraphContext.Provenance,
            provenance => provenance.EvidenceId == evidence.Id));
        Assert.Equal(EvidenceLevel.Proposed, result.EvidenceLevel);
        var task = Assert.Single(result.Tasks);
        Assert.Equal(EvidenceLevel.Proposed, task.EvidenceLevel);
        Assert.Equal(TaskProposalDisposition.Suggested, task.Disposition);
        Assert.All(task.ArtifactIds, id => Assert.Contains(provider.Context.GraphContext.Artifacts, item => item.Id == id));
        Assert.All(task.EvidenceIds, id => Assert.Contains(provider.Context.GraphContext.Evidence, item => item.Id == id));
    }

    [Fact]
    public async Task CodexProviderUsesManagedAccountAndStrictStructuredOutputWithoutCredentialContracts()
    {
        var expected = new AiImpactReasoningResult(
            "Structured impact",
            ImpactSeverity.Medium,
            EvidenceLevel.Inferred,
            0.8m,
            [],
            []);
        var client = new FakeCodexClient(
            new CodexAccountState("chatgpt", RequiresOpenAiAuth: true),
            JsonSerializer.Serialize(expected, AnalysisJson.Options));
        var provider = new CodexAppServerReasoningProvider(client);

        var result = await provider.AnalyzeImpactAsync(EmptyReasoningContext(), TestContext.Current.CancellationToken);

        Assert.Equal(expected.Summary, result.Summary);
        Assert.Equal(expected.Severity, result.Severity);
        Assert.Equal(expected.EvidenceLevel, result.EvidenceLevel);
        Assert.Equal(expected.Confidence, result.Confidence);
        Assert.Empty(result.EvidenceIds);
        Assert.Empty(result.Tasks);
        Assert.Equal(1, client.RunCalls);
        Assert.Contains("Targeted context", client.Prompt, StringComparison.Ordinal);
        Assert.Contains("\"projectId\": \"project-1\"", client.Prompt, StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Object, client.Schema.ValueKind);
        Assert.False(client.Schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Contains("inferred", client.Schema.GetProperty("properties")
            .GetProperty("evidenceLevel")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString()));
        Assert.DoesNotContain(typeof(CodexAppServerOptions).GetProperties(), property =>
            property.Name.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Cookie", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("ApiKey", StringComparison.OrdinalIgnoreCase));

        var unauthenticated = new FakeCodexClient(
            new CodexAccountState(null, RequiresOpenAiAuth: true),
            JsonSerializer.Serialize(expected, AnalysisJson.Options));
        await Assert.ThrowsAsync<CodexAuthenticationRequiredException>(() =>
            new CodexAppServerReasoningProvider(unauthenticated)
                .AnalyzeImpactAsync(EmptyReasoningContext(), TestContext.Current.CancellationToken));
        Assert.Equal(0, unauthenticated.RunCalls);
    }

    [Fact]
    public async Task ConfiguredCodexAppServerProcessCompletesManagedAccountHandshake()
    {
        var executable = Environment.GetEnvironmentVariable("TCFLOW_CODEX_EXECUTABLE");
        if (string.IsNullOrWhiteSpace(executable))
        {
            return;
        }

        var isolatedDirectory = Path.Combine(
            Path.GetTempPath(),
            $"tcflow-codex-account-{Guid.NewGuid():N}");
        try
        {
            await using var client = new CodexAppServerProcessClient(new CodexAppServerOptions(
                executable,
                isolatedDirectory));

            var account = await client.ReadAccountAsync(TestContext.Current.CancellationToken);

            Assert.False(account.RequiresOpenAiAuth && string.IsNullOrWhiteSpace(account.AccountType));
        }
        finally
        {
            if (Directory.Exists(isolatedDirectory))
            {
                Directory.Delete(isolatedDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void ProgressiveTrustRejectsUnauthorizedActionsAndLowConfidenceRemainsSuggestion()
    {
        var low = Proposal("low", 0.6m, EvidenceLevel.Proposed, TaskProposalDisposition.Suggested);
        var high = Proposal("high", 0.9m, EvidenceLevel.Inferred, TaskProposalDisposition.Suggested);
        var impact = new StructuredImpactPlan(
            "impact",
            ProjectId,
            RepositoryId,
            MismatchId,
            "Impact",
            ImpactSeverity.High,
            EvidenceLevel.Inferred,
            0.9m,
            ["change-1"],
            ["evidence-1"],
            [low, high]);
        var suggestOnly = Policy(
            AiTrustLevel.SuggestOnly,
            AiPermissionCodes.AnalysisRun,
            AiPermissionCodes.TaskSuggest);

        Assert.Throws<AiPolicyViolationException>(() =>
            TaskGenerationService.Prepare(impact, TaskGenerationMode.Create, suggestOnly));
        Assert.Throws<AiPolicyViolationException>(() =>
            AiActionAuthorizer.EnsureAllowed(suggestOnly, AiTaskAction.Update));

        var createPolicy = Policy(
            AiTrustLevel.CreateTasks,
            AiPermissionCodes.AnalysisRun,
            AiPermissionCodes.TaskSuggest,
            AiPermissionCodes.TaskCreate);
        var prepared = TaskGenerationService.Prepare(impact, TaskGenerationMode.Create, createPolicy);

        Assert.Equal(TaskProposalDisposition.Suggested, prepared.Single(task => task.Id == "low").Disposition);
        Assert.Equal(TaskProposalDisposition.Create, prepared.Single(task => task.Id == "high").Disposition);
    }

    [Fact]
    public void SuggestOnlyPolicyAuthorizesSuggestionLifecycleButNotAutomaticTaskCreation()
    {
        var service = new TaskReconciliationService();
        var now = DateTimeOffset.Parse("2026-08-23T00:00:00Z");
        var suggestion = Proposal(
            "suggestion",
            0.9m,
            EvidenceLevel.Inferred,
            TaskProposalDisposition.Suggested);
        var suggestOnly = Policy(
            AiTrustLevel.SuggestOnly,
            AiPermissionCodes.AnalysisRun,
            AiPermissionCodes.TaskSuggest);
        var createSuggestion = service.Reconcile(suggestion, [], now);

        Assert.Equal(AiTaskAction.Suggest, AiActionAuthorizer.RequiredAction(createSuggestion));
        AiActionAuthorizer.EnsureAllowed(
            suggestOnly,
            AiActionAuthorizer.RequiredAction(createSuggestion));
        var suggestedTask = Assert.Single(createSuggestion.Mutations).After;
        Assert.Equal(SourceAwareTaskStatus.Suggested, suggestedTask.Status);

        var cancelSuggestion = service.Reconcile(
            suggestion with { ChangeState = SourceChangeState.Reverted },
            [suggestedTask],
            now.AddMinutes(1));
        Assert.Equal(AiTaskAction.Suggest, AiActionAuthorizer.RequiredAction(cancelSuggestion));
        var cancelledTask = Assert.Single(cancelSuggestion.Mutations).After;
        var reopenSuggestion = service.Reconcile(
            suggestion,
            [cancelledTask],
            now.AddMinutes(2));
        Assert.Equal(SourceAwareTaskStatus.Suggested, Assert.Single(reopenSuggestion.Mutations).After.Status);
        Assert.Equal(AiTaskAction.Suggest, AiActionAuthorizer.RequiredAction(reopenSuggestion));

        var automatic = service.Reconcile(
            suggestion with { Disposition = TaskProposalDisposition.Create },
            [],
            now);
        Assert.Equal(AiTaskAction.Create, AiActionAuthorizer.RequiredAction(automatic));
        Assert.Throws<AiPolicyViolationException>(() => AiActionAuthorizer.EnsureAllowed(
            suggestOnly,
            AiActionAuthorizer.RequiredAction(automatic)));

        var promote = service.Reconcile(
            suggestion with { Disposition = TaskProposalDisposition.Create },
            [suggestedTask],
            now.AddMinutes(3));
        Assert.Equal(AiTaskAction.Create, AiActionAuthorizer.RequiredAction(promote));
        var createPolicy = Policy(
            AiTrustLevel.CreateTasks,
            AiPermissionCodes.AnalysisRun,
            AiPermissionCodes.TaskSuggest,
            AiPermissionCodes.TaskCreate);
        AiActionAuthorizer.EnsureAllowed(createPolicy, AiActionAuthorizer.RequiredAction(promote));
        Assert.Equal(SourceAwareTaskStatus.Upcoming, Assert.Single(promote.Mutations).After.Status);
    }

    [Fact]
    public async Task MartenPersistsSuggestedTaskWithSuggestOnlyPolicyAndSuggestionAudit()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        using var store = DocumentStore.For(options =>
        {
            options.Connection(postgres.GetConnectionString());
            options.DatabaseSchemaName = "suggestion_policy_test";
            TaskReconciliationStorage.Configure(options);
        });
        await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
        var proposal = Proposal(
            "suggested-persistence",
            0.9m,
            EvidenceLevel.Inferred,
            TaskProposalDisposition.Suggested);
        var decision = new TaskReconciliationService().Reconcile(
            proposal,
            [],
            DateTimeOffset.UtcNow);
        var policy = Policy(
            AiTrustLevel.SuggestOnly,
            AiPermissionCodes.AnalysisRun,
            AiPermissionCodes.TaskSuggest);

        await using (var session = store.LightweightSession())
        {
            await new MartenTaskReconciliationWriter(session, TimeProvider.System).ApplyAsync(
                decision,
                policy,
                "ai:codex",
                TestContext.Current.CancellationToken);
        }

        await using var query = store.QuerySession();
        var task = Assert.Single(await query.Query<SourceAwareEngineeringTask>()
            .ToListAsync(TestContext.Current.CancellationToken));
        var audit = Assert.Single(await query.Query<AiActionAudit>()
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(SourceAwareTaskStatus.Suggested, task.Status);
        Assert.Equal(AiPermissionCodes.TaskSuggest, audit.Action);
    }

    [Fact]
    public async Task ReconciliationCoversCanonicalCreateUpdateMergeCloseReopenAndIgnoreCases()
    {
        var expected = JsonSerializer.Deserialize<ExpectedReconciliationActions>(
            await File.ReadAllTextAsync(
                Path.Combine(RepositoryRoot, "samples", "reasoning", "expected", "reconciliation.json"),
                TestContext.Current.CancellationToken),
            AnalysisJson.Options)!;
        var service = new TaskReconciliationService();
        var now = DateTimeOffset.Parse("2026-08-20T00:00:00Z");
        var proposal = Proposal("proposal", 0.9m, EvidenceLevel.Inferred, TaskProposalDisposition.Create);
        var create = service.Reconcile(proposal, [], now);
        var original = Assert.Single(create.Mutations).After;
        var ignore = service.Reconcile(proposal, [original], now.AddMinutes(1));
        var update = service.Reconcile(
            proposal with { Requirements = ["requirement", "new requirement"] },
            [original],
            now.AddMinutes(2));
        var duplicate = original with { Id = "duplicate-task", CreatedAt = now.AddSeconds(1) };
        var merge = service.Reconcile(proposal, [original, duplicate], now.AddMinutes(3));
        var close = service.Reconcile(
            proposal with { ChangeState = SourceChangeState.Reverted },
            [original],
            now.AddMinutes(4));
        var cancelled = original with { Status = SourceAwareTaskStatus.Cancelled };
        var reopen = service.Reconcile(proposal, [cancelled], now.AddMinutes(5));
        var completedRevert = service.Reconcile(
            proposal with { ChangeState = SourceChangeState.Reverted },
            [original with { Status = SourceAwareTaskStatus.Completed }],
            now.AddMinutes(6));

        Assert.Equal(expected.Create, create.Action);
        Assert.Equal(expected.Ignore, ignore.Action);
        Assert.Equal(expected.Update, update.Action);
        Assert.Equal(expected.Merge, merge.Action);
        Assert.Equal(expected.Close, close.Action);
        Assert.Equal(expected.Reopen, reopen.Action);
        Assert.Equal(expected.CompletedRevert, completedRevert.Action);
        Assert.True(completedRevert.RequiresHumanReview);
        Assert.Empty(completedRevert.Mutations);
        Assert.Equal(2, merge.Mutations.Count);
        Assert.Single(merge.Mutations, mutation => mutation.After.MergedIntoTaskId == original.Id);
        Assert.All(close.Mutations, mutation => Assert.Equal(SourceAwareTaskStatus.Cancelled, mutation.After.Status));
    }

    [Fact]
    public async Task MartenPersistsTaskVersionsAndAuditsWhileRejectingUnauthorizedClose()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        using var store = DocumentStore.For(options =>
        {
            options.Connection(postgres.GetConnectionString());
            options.DatabaseSchemaName = "reasoning_test";
            TaskReconciliationStorage.Configure(options);
        });
        await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
        var service = new TaskReconciliationService();
        var proposal = Proposal("persisted", 0.9m, EvidenceLevel.Inferred, TaskProposalDisposition.Create);
        var fullPolicy = Policy(
            AiTrustLevel.UpdateTasks,
            AiPermissionCodes.AnalysisRun,
            AiPermissionCodes.TaskSuggest,
            AiPermissionCodes.TaskCreate,
            AiPermissionCodes.TaskUpdate,
            AiPermissionCodes.TaskClose);
        var create = service.Reconcile(proposal, [], DateTimeOffset.UtcNow);

        await using (var session = store.LightweightSession())
        {
            await new MartenTaskReconciliationWriter(session, TimeProvider.System).ApplyAsync(
                create,
                fullPolicy,
                "ai:codex",
                TestContext.Current.CancellationToken);
        }

        SourceAwareEngineeringTask current;
        await using (var session = store.QuerySession())
        {
            var reader = new MartenTaskReconciliationReader(session);
            current = Assert.Single(await reader.FindRelatedAsync(
                ProjectId,
                RepositoryId,
                proposal.CorrelationKey,
                TestContext.Current.CancellationToken));
            Assert.Single(await reader.GetHistoryAsync(
                ProjectId,
                current.Id,
                TestContext.Current.CancellationToken));
            Assert.Single(await reader.FindBySourceChangesAsync(
                ProjectId,
                RepositoryId,
                proposal.SourceChangeIds,
                TestContext.Current.CancellationToken));
            Assert.Single(await session.Query<AiActionAudit>().ToListAsync(TestContext.Current.CancellationToken));
        }

        var changedProposal = proposal with { Requirements = ["requirement", "persist version two"] };
        var update = service.Reconcile(changedProposal, [current], DateTimeOffset.UtcNow);
        await using (var session = store.LightweightSession())
        {
            await new MartenTaskReconciliationWriter(session, TimeProvider.System).ApplyAsync(
                update,
                fullPolicy,
                "ai:codex",
                TestContext.Current.CancellationToken);
        }

        await using (var session = store.QuerySession())
        {
            current = Assert.Single(await new MartenTaskReconciliationReader(session).FindRelatedAsync(
                ProjectId,
                RepositoryId,
                proposal.CorrelationKey,
                TestContext.Current.CancellationToken));
            Assert.Equal(2, current.Version);
        }

        var close = service.Reconcile(
            changedProposal with { ChangeState = SourceChangeState.Reverted },
            [current],
            DateTimeOffset.UtcNow);
        var createOnlyPolicy = Policy(
            AiTrustLevel.CreateTasks,
            AiPermissionCodes.AnalysisRun,
            AiPermissionCodes.TaskSuggest,
            AiPermissionCodes.TaskCreate);
        await using (var session = store.LightweightSession())
        {
            await Assert.ThrowsAsync<AiPolicyViolationException>(() =>
                new MartenTaskReconciliationWriter(session, TimeProvider.System).ApplyAsync(
                    close,
                    createOnlyPolicy,
                    "ai:codex",
                    TestContext.Current.CancellationToken));
        }

        await using (var session = store.QuerySession())
        {
            var reader = new MartenTaskReconciliationReader(session);
            Assert.Equal(2, (await reader.GetHistoryAsync(
                ProjectId,
                current.Id,
                TestContext.Current.CancellationToken)).Count);
            Assert.Equal(2, (await session.Query<AiActionAudit>()
                .ToListAsync(TestContext.Current.CancellationToken)).Count);
        }

        await using (var session = store.LightweightSession())
        {
            await new MartenTaskReconciliationWriter(session, TimeProvider.System).ApplyAsync(
                close,
                fullPolicy,
                "ai:codex",
                TestContext.Current.CancellationToken);
        }

        await using (var session = store.QuerySession())
        {
            var reader = new MartenTaskReconciliationReader(session);
            var closed = Assert.Single(await reader.FindRelatedAsync(
                ProjectId,
                RepositoryId,
                proposal.CorrelationKey,
                TestContext.Current.CancellationToken));
            Assert.Equal(SourceAwareTaskStatus.Cancelled, closed.Status);
            Assert.Equal(3, (await reader.GetHistoryAsync(
                ProjectId,
                closed.Id,
                TestContext.Current.CancellationToken)).Count);
            Assert.Equal(3, (await session.Query<AiActionAudit>()
                .ToListAsync(TestContext.Current.CancellationToken)).Count);
        }
    }

    private static async Task<ReasoningFixture> BuildFixture()
    {
        var vueFiles = await Discover("vue-full-application", ".vue", ".ts");
        var aspNetFiles = await Discover("aspnet-full-application", ".cs");
        var martenFiles = await Discover("marten-full-application", ".cs");
        var vue = await new VueAnalyzer().AnalyzeAsync(vueFiles, TestContext.Current.CancellationToken);
        var api = vue.Artifacts.Single(artifact =>
            artifact.Kind == ArtifactKind.ApiCall && artifact.Name == "POST /api/products");
        var change = new SourceChange(
            StableIdentity.Create("change", "src/views/CreateProductView.vue", "reasoning-fixture"),
            "src/views/CreateProductView.vue",
            ChangeKind.Modified,
            "before",
            "after",
            true,
            "Frontend request contract changed.");
        var impact = new Impact(
            StableIdentity.Create("impact", change.Id, api.Id),
            change.Id,
            api.Id,
            ImpactSeverity.High,
            "The changed API request can affect downstream contracts.",
            0.95m,
            EvidenceLevel.Inferred,
            api.EvidenceIds);
        vue = vue with { Changes = [change], Impacts = [impact] };
        var aspNet = await new AspNetAnalyzer().AnalyzeAsync(aspNetFiles, TestContext.Current.CancellationToken);
        var marten = await new MartenAnalyzer().AnalyzeAsync(martenFiles, TestContext.Current.CancellationToken);
        var graph = new RepositoryKnowledgeGraphAssembler().Build(RepositoryId, [vue, aspNet, marten]);
        var mismatch = graph.ContractMismatches.Single(item =>
            item.Kind == ContractMismatchKind.RequestFieldMissingBackend && item.Subject == "categoryId");
        var authority = new AuthorityImpactEvaluator().Evaluate(
            mismatch,
            new RepositoryAuthorityPolicy(
                ProjectId,
                1,
                IsConfigured: true,
                [
                    new KnowledgeAuthorityRule(
                        AuthorityKnowledgeKind.ApiContract,
                        AuthoritySourceKind.Frontend,
                        EvidenceLevel.Confirmed,
                        1m,
                        "Project-configured authority.",
                        [])
                ]));
        return new ReasoningFixture(
            graph,
            change,
            authority,
            new ConventionDetector().Detect(graph));
    }

    private static AiReasoningContext EmptyReasoningContext() => new(
        ProjectId,
        RepositoryId,
        ["change-1"],
        new RetrievalContext(RepositoryId, [], [], [], [], [], [], [], [], [], [], []),
        new AuthorityImpactDecision(
            "authority-1",
            MismatchId,
            AuthorityKnowledgeKind.ApiContract,
            AuthoritySourceKind.Frontend,
            AuthorityImpactAction.AlignBackendToFrontend,
            PlanTargetComponent.Backend,
            EvidenceLevel.Inferred,
            0.8m,
            "Frontend is authoritative.",
            []),
        []);

    private static StructuredTaskProposal Proposal(
        string id,
        decimal confidence,
        EvidenceLevel evidenceLevel,
        TaskProposalDisposition disposition) => new(
        id,
        ProjectId,
        RepositoryId,
        "correlation-1",
        MismatchId,
        "Align request contract",
        "Keep frontend and backend request fields aligned.",
        PlanTargetComponent.Backend,
        evidenceLevel,
        confidence,
        ["artifact-1"],
        ["evidence-1"],
        ["change-1"],
        ["requirement"],
        SourceChangeState.Active,
        disposition);

    private static AiActionPolicy Policy(AiTrustLevel level, params string[] permissions) =>
        new(ProjectId, level, permissions);

    private static Task<IReadOnlyList<RepositoryFile>> Discover(string fixture, params string[] extensions) =>
        new FileDiscovery().DiscoverAsync(
            Path.Combine(RepositoryRoot, "samples", fixture),
            new FileDiscoveryOptions(new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase)),
            TestContext.Current.CancellationToken);

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PROJECT_PLAN.md")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
        }
    }

    private const string ProjectId = "project-1";
    private const string RepositoryId = "reasoning-fixture";
    private const string MismatchId = "mismatch-1";

    private sealed record ReasoningFixture(
        RepositoryKnowledgeGraph Graph,
        SourceChange Change,
        AuthorityImpactDecision Authority,
        RepositoryConventionProfile Conventions);

    private sealed record ExpectedReconciliationActions(
        TaskReconciliationAction Create,
        TaskReconciliationAction Ignore,
        TaskReconciliationAction Update,
        TaskReconciliationAction Merge,
        TaskReconciliationAction Close,
        TaskReconciliationAction Reopen,
        TaskReconciliationAction CompletedRevert);

    private sealed class CapturingReasoningProvider(
        Func<AiReasoningContext, AiImpactReasoningResult> resultFactory) : IAiReasoningProvider
    {
        public AiReasoningContext Context { get; private set; } = null!;

        public Task<AiImpactReasoningResult> AnalyzeImpactAsync(
            AiReasoningContext context,
            CancellationToken cancellationToken = default)
        {
            Context = context;
            return Task.FromResult(resultFactory(context));
        }
    }

    private sealed class FakeCodexClient(CodexAccountState account, string output) : ICodexAppServerClient
    {
        public int RunCalls { get; private set; }

        public string Prompt { get; private set; } = string.Empty;

        public JsonElement Schema { get; private set; }

        public Task<CodexAccountState> ReadAccountAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(account);

        public Task<string> RunStructuredTurnAsync(
            string prompt,
            JsonElement outputSchema,
            CancellationToken cancellationToken = default)
        {
            RunCalls++;
            Prompt = prompt;
            Schema = outputSchema.Clone();
            return Task.FromResult(output);
        }
    }
}
