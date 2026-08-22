using System.Text.Json;
using VietAIS.TCFlow.Analyzers.AspNet;
using VietAIS.TCFlow.Analyzers.Contracts;
using VietAIS.TCFlow.Analyzers.Core;
using VietAIS.TCFlow.Analyzers.Governance;
using VietAIS.TCFlow.Analyzers.Knowledge;
using VietAIS.TCFlow.Analyzers.Marten;
using VietAIS.TCFlow.Analyzers.Monitoring;
using VietAIS.TCFlow.Analyzers.Reasoning;
using VietAIS.TCFlow.Analyzers.Vue;
using Xunit;

namespace VietAIS.TCFlow.Analyzers.Monitoring.Tests;

public sealed class IncrementalMonitoringTests
{
    [Fact]
    public async Task CosmeticOnlyCommitDoesNotUpdateGraphQueueAiOrCreateTasks()
    {
        var fixture = await BuildFixtureAsync();
        var before = fixture.BeforeTarget.Content;
        var after = before.Replace("gap: 1rem", "gap: 2rem", StringComparison.Ordinal);
        var queue = new InMemoryDeepReasoningQueue();
        var service = Service(
            new StaticChangeSource(ChangeSet(fixture.BeforeTarget, before, after)),
            new InMemoryIncrementalDeliveryRegistry(),
            queue);

        var result = await service.ProcessAsync(
            WorkItem("cosmetic", fixture.BeforeTarget.RelativePath),
            fixture.Graph,
            TestContext.Current.CancellationToken);

        Assert.Equal(IncrementalMonitoringStatus.Ignored, result.Status);
        Assert.Equal(fixture.Graph.Revision, result.Graph.Revision);
        Assert.Empty(result.Impacts);
        Assert.Empty(queue.WorkItems);
        Assert.Equal(ChangeDecision.CosmeticOnly, Assert.Single(result.Changes).Decision);
        Assert.Equal(Acceptance.CosmeticAiRequests, queue.WorkItems.Count);
    }

    [Fact]
    public async Task ChangedPathIsReparsedWithoutDroppingUnchangedAnalyzerArtifactsAndQueuesTargetedReasoning()
    {
        var fixture = await BuildFixtureAsync();
        var queue = new InMemoryDeepReasoningQueue();
        var service = Service(
            new StaticChangeSource(ChangeSet(
                fixture.AfterTarget,
                fixture.BeforeTarget.Content,
                fixture.AfterTarget.Content)),
            new InMemoryIncrementalDeliveryRegistry(),
            queue);
        var unchangedVueArtifacts = fixture.Graph.Artifacts
            .Where(artifact => artifact.Technology == "vue" && artifact.Path != fixture.AfterTarget.RelativePath)
            .Select(artifact => artifact.Id)
            .ToArray();

        var result = await service.ProcessAsync(
            WorkItem("meaningful", fixture.AfterTarget.RelativePath),
            fixture.Graph,
            TestContext.Current.CancellationToken);

        Assert.Equal(IncrementalMonitoringStatus.DeepReasoningQueued, result.Status);
        Assert.Equal(fixture.Graph.Revision + 1, result.Graph.Revision);
        Assert.NotEmpty(result.Impacts);
        Assert.Contains(result.Graph.ContractMismatches, mismatch =>
            mismatch.Kind == ContractMismatchKind.RequestFieldMissingBackend && mismatch.Subject == "categoryId");
        Assert.All(unchangedVueArtifacts, id => Assert.Contains(result.Graph.Artifacts, artifact => artifact.Id == id));
        Assert.Single(queue.WorkItems);
        Assert.Equal(result.DeepReasoning, queue.WorkItems[0]);
        Assert.NotEmpty(result.DeepReasoning!.ContractMismatchIds);
        Assert.All(result.DeepReasoning.ContractMismatchIds, id => Assert.Equal(
            "categoryId",
            result.Graph.ContractMismatches.Single(mismatch => mismatch.Id == id).Subject));
        Assert.All(result.Impacts, impact => Assert.Contains(result.Graph.Changes,
            change => change.Id == impact.SourceChangeId));
    }

    [Fact]
    public async Task ConcurrentDuplicateDeliveryProducesOneGraphUpdateAndOneReasoningJob()
    {
        var fixture = await BuildFixtureAsync();
        var registry = new InMemoryIncrementalDeliveryRegistry();
        var queue = new InMemoryDeepReasoningQueue();
        var service = Service(
            new StaticChangeSource(ChangeSet(
                fixture.AfterTarget,
                fixture.BeforeTarget.Content,
                fixture.AfterTarget.Content)),
            registry,
            queue);
        var workItem = WorkItem("duplicate", fixture.AfterTarget.RelativePath);

        var results = await Task.WhenAll(
            service.ProcessAsync(workItem, fixture.Graph, TestContext.Current.CancellationToken),
            service.ProcessAsync(workItem, fixture.Graph, TestContext.Current.CancellationToken));

        var accepted = Assert.Single(results, result => result.Status == IncrementalMonitoringStatus.DeepReasoningQueued);
        Assert.Single(results, result => result.Status == IncrementalMonitoringStatus.Duplicate);
        Assert.Single(queue.WorkItems);
        Assert.Equal(
            Acceptance.DuplicateChanges,
            accepted.Graph.Changes.GroupBy(change => change.Id, StringComparer.Ordinal).Count(group => group.Count() > 1));
        Assert.Equal(Acceptance.DuplicateTasks, queue.WorkItems.GroupBy(job => job.Id).Count(group => group.Count() > 1));
    }

    [Fact]
    public async Task DeferredPullRequestFileListIsLoadedThroughTheChangeSourceBoundary()
    {
        var fixture = await BuildFixtureAsync();
        var queue = new InMemoryDeepReasoningQueue();
        var service = Service(
            new StaticChangeSource(ChangeSet(
                fixture.AfterTarget,
                fixture.BeforeTarget.Content,
                fixture.AfterTarget.Content)),
            new InMemoryIncrementalDeliveryRegistry(),
            queue);
        var workItem = WorkItem("pull-request") with
        {
            Trigger = RepositoryAnalysisTrigger.PullRequest,
            PullRequestNumber = 42,
            RequiresContentFetch = true,
            ChangedPaths = []
        };

        var result = await service.ProcessAsync(
            workItem,
            fixture.Graph,
            TestContext.Current.CancellationToken);

        Assert.Equal(IncrementalMonitoringStatus.DeepReasoningQueued, result.Status);
        Assert.Single(queue.WorkItems);
        Assert.Contains(result.Graph.Changes, change => change.Path == fixture.AfterTarget.RelativePath);
    }

    [Fact]
    public async Task RevertIsDetectedAndDeepProcessorCreatesIgnoresThenClosesWithoutCallingAiForRevert()
    {
        var fixture = await BuildFixtureAsync();
        var registry = new InMemoryIncrementalDeliveryRegistry();
        var queue = new InMemoryDeepReasoningQueue();
        var forward = await Service(
                new StaticChangeSource(ChangeSet(
                    fixture.AfterTarget,
                    fixture.BeforeTarget.Content,
                    fixture.AfterTarget.Content)),
                registry,
                queue)
            .ProcessAsync(
                WorkItem("forward", fixture.AfterTarget.RelativePath),
                fixture.Graph,
                TestContext.Current.CancellationToken);
        var forwardJob = Assert.IsType<DeepReasoningWorkItem>(forward.DeepReasoning);
        var provider = new ContextBackedReasoningProvider();
        var tasks = new InMemoryTaskGateway();
        var processor = new IncrementalDeepReasoningProcessor(
            provider,
            tasks,
            timeProvider: TimeProvider.System);
        var settings = Settings(forward.Graph);

        var created = await processor.ProcessAsync(
            forwardJob,
            forward.Graph,
            settings,
            TestContext.Current.CancellationToken);
        var taskCount = tasks.Tasks.Count;
        var ignored = await processor.ProcessAsync(
            forwardJob,
            forward.Graph,
            settings,
            TestContext.Current.CancellationToken);

        Assert.NotEmpty(created.Decisions);
        Assert.All(created.Decisions, decision => Assert.Equal(TaskReconciliationAction.Create, decision.Action));
        Assert.All(ignored.Decisions, decision => Assert.Equal(TaskReconciliationAction.Ignore, decision.Action));
        Assert.Equal(taskCount, tasks.Tasks.Count);

        var reverted = await Service(
                new StaticChangeSource(ChangeSet(
                    fixture.BeforeTarget,
                    fixture.AfterTarget.Content,
                    fixture.BeforeTarget.Content)),
                registry,
                queue)
            .ProcessAsync(
                WorkItem("revert", fixture.BeforeTarget.RelativePath),
                forward.Graph,
                TestContext.Current.CancellationToken);
        var revertJob = Assert.IsType<DeepReasoningWorkItem>(reverted.DeepReasoning);
        Assert.Contains(forwardJob.SourceChangeIds[0], revertJob.RevertedSourceChangeIds);
        var providerCallsBeforeRevert = provider.Calls;

        var closed = await processor.ProcessAsync(
            revertJob,
            reverted.Graph,
            Settings(reverted.Graph),
            TestContext.Current.CancellationToken);

        Assert.Equal(providerCallsBeforeRevert, provider.Calls);
        Assert.NotEmpty(closed.Decisions);
        Assert.All(closed.Decisions, decision => Assert.Equal(TaskReconciliationAction.Close, decision.Action));
        Assert.All(tasks.Tasks, task => Assert.Equal(SourceAwareTaskStatus.Cancelled, task.Status));
        var decisions = created.Decisions.Concat(ignored.Decisions).Concat(closed.Decisions).ToArray();
        var correct = created.Decisions.Count(decision => decision.Action == TaskReconciliationAction.Create) +
            ignored.Decisions.Count(decision => decision.Action == TaskReconciliationAction.Ignore) +
            closed.Decisions.Count(decision => decision.Action == TaskReconciliationAction.Close);
        var accuracy = decimal.Divide(correct, decisions.Length);
        Assert.Equal(Acceptance.CanonicalReconciliationAccuracy, accuracy);
    }

    [Fact]
    public async Task DeterministicFastPathP95StaysUnderTargetForTwentyChangedFiles()
    {
        var paths = Enumerable.Range(1, Acceptance.MaximumChangedFiles)
            .Select(index => $"src/features/feature-{index}.ts")
            .ToArray();
        var changes = paths.Select(path => new SourceFileChange(
                path,
                "export const value = 1",
                "export async function save() { return api.post('/api/items', {}) }"))
            .ToArray();
        var files = changes.Select(change => new RepositoryFile(change.Path, change.Path, change.After!)).ToArray();
        var source = new StaticChangeSource(new IncrementalChangeSet(changes, files));
        var graph = new RepositoryKnowledgeGraphAssembler().Build(RepositoryId, []);
        var service = Service(source, new InMemoryIncrementalDeliveryRegistry(), new InMemoryDeepReasoningQueue());
        var samples = new List<double>();

        for (var iteration = 0; iteration < 35; iteration++)
        {
            var result = await service.ProcessAsync(
                WorkItem($"benchmark-{iteration}", paths),
                graph,
                TestContext.Current.CancellationToken);
            if (iteration >= 5)
            {
                samples.Add(result.Elapsed.TotalMilliseconds);
            }
        }

        samples.Sort();
        var p95 = samples[(int)Math.Ceiling(samples.Count * 0.95) - 1];
        Console.WriteLine(
            $"P13 deterministic fast-path p95: {p95:F2} ms for {Acceptance.MaximumChangedFiles} changed files.");
        Assert.True(
            p95 < Acceptance.MaximumP95Milliseconds,
            $"Incremental deterministic p95 was {p95:F2} ms; target is under {Acceptance.MaximumP95Milliseconds} ms.");
    }

    private static IncrementalMonitoringService Service(
        IIncrementalChangeSource source,
        IIncrementalDeliveryRegistry registry,
        IDeepReasoningQueue queue) => new(
        source,
        registry,
        queue,
        [new VueAnalyzer(), new AspNetAnalyzer(), new MartenAnalyzer()],
        TimeProvider.System);

    private static IncrementalChangeSet ChangeSet(RepositoryFile afterFile, string before, string after) => new(
        [new SourceFileChange(afterFile.RelativePath, before, after)],
        [afterFile with { Content = after }]);

    private static RepositoryAnalysisWorkItem WorkItem(string id, params string[] paths) => new(
        id,
        ProjectId,
        RepositoryId,
        $"delivery-{id}",
        "github",
        RepositoryAnalysisKind.Incremental,
        RepositoryAnalysisTrigger.Push,
        "before",
        "after",
        "refs/heads/main",
        null,
        RequiresContentFetch: false,
        paths.Select(path => new RepositoryChangedPath(path, ChangeKind.Modified)).ToArray(),
        DateTimeOffset.Parse("2026-08-22T00:00:00Z"),
        RepositoryAnalysisRequesterKind.System,
        null);

    private static IncrementalDeepReasoningSettings Settings(RepositoryKnowledgeGraph graph) => new(
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
                    "Fixture policy makes frontend authoritative.",
                    [])
            ]),
        new ConventionDetector().Detect(graph),
        new AiActionPolicy(
            ProjectId,
            AiTrustLevel.UpdateTasks,
            [
                AiPermissionCodes.AnalysisRun,
                AiPermissionCodes.TaskSuggest,
                AiPermissionCodes.TaskCreate,
                AiPermissionCodes.TaskUpdate,
                AiPermissionCodes.TaskClose
            ]),
        TaskGenerationMode.Create,
        "ai:incremental-monitoring");

    private static async Task<Fixture> BuildFixtureAsync()
    {
        var vueFiles = await DiscoverAsync("vue-full-application", ".vue", ".ts");
        var afterTarget = vueFiles.Single(file => file.RelativePath.EndsWith("CreateProductView.vue",
            StringComparison.Ordinal));
        var beforeContent = RemoveCategory(afterTarget.Content);
        var beforeTarget = afterTarget with { Content = beforeContent };
        var beforeVue = vueFiles.Select(file => file.RelativePath == beforeTarget.RelativePath ? beforeTarget : file)
            .ToArray();
        var aspNetFiles = await DiscoverAsync("aspnet-full-application", ".cs");
        var martenFiles = await DiscoverAsync("marten-full-application", ".cs");
        var vue = await new VueAnalyzer().AnalyzeAsync(beforeVue, TestContext.Current.CancellationToken);
        var aspNet = await new AspNetAnalyzer().AnalyzeAsync(aspNetFiles, TestContext.Current.CancellationToken);
        var marten = await new MartenAnalyzer().AnalyzeAsync(martenFiles, TestContext.Current.CancellationToken);
        var graph = new RepositoryKnowledgeGraphAssembler().Build(RepositoryId, [vue, aspNet, marten]);
        Assert.DoesNotContain(graph.ContractMismatches, mismatch => mismatch.Subject == "categoryId");
        return new Fixture(graph, beforeTarget, afterTarget);
    }

    private static string RemoveCategory(string source) => source
        .Replace("    <select v-model=\"categoryId\" required>\n      <option value=\"\">Select a category</option>\n    </select>\n", string.Empty, StringComparison.Ordinal)
        .Replace("  categoryId: string\n", string.Empty, StringComparison.Ordinal)
        .Replace("  initialCategoryId?: string\n", string.Empty, StringComparison.Ordinal)
        .Replace("const categoryId = ref(props.initialCategoryId ?? '')\n", string.Empty, StringComparison.Ordinal)
        .Replace("    categoryId: categoryId.value,\n", string.Empty, StringComparison.Ordinal);

    private static Task<IReadOnlyList<RepositoryFile>> DiscoverAsync(string fixture, params string[] extensions) =>
        new FileDiscovery().DiscoverAsync(
            Path.Combine(RepositoryRoot, "samples", fixture),
            new FileDiscoveryOptions(new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase)),
            TestContext.Current.CancellationToken);

    private static AcceptanceTargets Acceptance { get; } = JsonSerializer.Deserialize<AcceptanceTargets>(
        File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "samples",
            "incremental-monitoring",
            "expected",
            "acceptance.json")),
        AnalysisJson.Options)!;

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
    private const string RepositoryId = "incremental-fixture";

    private sealed record Fixture(
        RepositoryKnowledgeGraph Graph,
        RepositoryFile BeforeTarget,
        RepositoryFile AfterTarget);

    private sealed record AcceptanceTargets(
        int MaximumChangedFiles,
        double MaximumP95Milliseconds,
        int CosmeticAiRequests,
        int DuplicateChanges,
        int DuplicateTasks,
        decimal CanonicalReconciliationAccuracy);

    private sealed class StaticChangeSource(IncrementalChangeSet changeSet) : IIncrementalChangeSource
    {
        public Task<IncrementalChangeSet> LoadAsync(
            RepositoryAnalysisWorkItem workItem,
            CancellationToken cancellationToken = default) => Task.FromResult(changeSet);
    }

    private sealed class ContextBackedReasoningProvider : IAiReasoningProvider
    {
        public int Calls { get; private set; }

        public Task<AiImpactReasoningResult> AnalyzeImpactAsync(
            AiReasoningContext context,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            var artifact = context.GraphContext.Artifacts.First();
            var evidence = context.GraphContext.Evidence.First();
            return Task.FromResult(new AiImpactReasoningResult(
                "The authoritative frontend request changed and the backend contract must align.",
                ImpactSeverity.High,
                EvidenceLevel.Inferred,
                0.95m,
                [evidence.Id],
                [
                    new AiTaskReasoningResult(
                        "Align backend request contract",
                        "Update the backend contract using targeted source evidence.",
                        PlanTargetComponent.Backend,
                        EvidenceLevel.Inferred,
                        0.95m,
                        [artifact.Id],
                        [evidence.Id],
                        ["Accept categoryId in the backend request contract."])
                ]));
        }
    }

    private sealed class InMemoryTaskGateway : IIncrementalTaskGateway
    {
        private readonly Dictionary<string, SourceAwareEngineeringTask> _tasks = new(StringComparer.Ordinal);

        public IReadOnlyList<SourceAwareEngineeringTask> Tasks => _tasks.Values.OrderBy(task => task.Id).ToArray();

        public Task<IReadOnlyList<SourceAwareEngineeringTask>> FindRelatedAsync(
            string projectId,
            string repositoryId,
            string correlationKey,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SourceAwareEngineeringTask>>(
            _tasks.Values.Where(task => task.ProjectId == projectId &&
                    task.RepositoryId == repositoryId && task.CorrelationKey == correlationKey)
                .ToArray());

        public Task<IReadOnlyList<SourceAwareEngineeringTask>> FindBySourceChangesAsync(
            string projectId,
            string repositoryId,
            IReadOnlyCollection<string> sourceChangeIds,
            CancellationToken cancellationToken = default)
        {
            var ids = sourceChangeIds.ToHashSet(StringComparer.Ordinal);
            return Task.FromResult<IReadOnlyList<SourceAwareEngineeringTask>>(_tasks.Values.Where(task =>
                    task.ProjectId == projectId && task.RepositoryId == repositoryId &&
                    task.SourceChangeIds.Any(ids.Contains))
                .ToArray());
        }

        public Task ApplyAsync(
            TaskReconciliationDecision decision,
            AiActionPolicy policy,
            string actorId,
            CancellationToken cancellationToken = default)
        {
            foreach (var mutation in decision.Mutations)
            {
                _tasks[mutation.After.Id] = mutation.After;
            }

            return Task.CompletedTask;
        }
    }
}
