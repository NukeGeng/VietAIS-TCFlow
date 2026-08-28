using System.Text.Json;
using VietAIS.TCFlow.Analyzers.AspNet;
using VietAIS.TCFlow.Analyzers.Contracts;
using VietAIS.TCFlow.Analyzers.Core;
using VietAIS.TCFlow.Analyzers.Knowledge;
using VietAIS.TCFlow.Analyzers.Marten;
using VietAIS.TCFlow.Analyzers.Monitoring;
using VietAIS.TCFlow.Analyzers.Reasoning;
using VietAIS.TCFlow.Analyzers.Vue;
using Xunit;

namespace VietAIS.TCFlow.Analyzers.Monitoring.Tests;

public sealed class EndToEndQualityBenchmarkTests
{
    [Fact]
    public async Task SupportedVerticalSliceMeetsP14QualityTargets()
    {
        var targets = await LoadTargetsAsync();
        var files = await DiscoverFixtureAsync();
        var initial = await new InitialRepositoryAnalysisService(
                new StaticSnapshotSource(new RepositorySnapshot("fixture-revision", files)),
                [new VueAnalyzer(), new AspNetAnalyzer(), new MartenAnalyzer()])
            .ProcessAsync(InitialWorkItem(), graphRevision: 1, TestContext.Current.CancellationToken);

        Assert.Equal(InitialRepositoryAnalysisStatus.Completed, initial.Status);
        var apiCall = initial.Graph.Artifacts.Single(artifact =>
            artifact.Kind == ArtifactKind.ApiCall &&
            artifact.Name == "POST /api/v1/catalog/products");
        var context = new KnowledgeRetriever().RetrieveForArtifacts(initial.Graph, [apiCall.Id], maxDepth: 2);
        var observations = targets.Facts.Select(fact => new FactObservation(
                fact.Id,
                fact.ExpectedPresent,
                IsPresent(fact, initial, context)))
            .ToArray();
        var classification = Classify(observations);
        var taskMetrics = MeasureTaskReconciliation();
        var fastPathP95 = await MeasureFastPathP95Async(targets.MaximumChangedFiles);
        var report = new QualityBenchmarkReport(
            classification.Precision,
            classification.Recall,
            classification.FalsePositiveRate,
            classification.FalseNegativeRate,
            taskMetrics.DuplicationRate,
            taskMetrics.Accuracy,
            fastPathP95,
            classification.TruePositive,
            classification.FalsePositive,
            classification.TrueNegative,
            classification.FalseNegative,
            targets.Facts.Count,
            DateTimeOffset.UtcNow);

        Console.WriteLine("P14_QUALITY_REPORT=" + JsonSerializer.Serialize(report, AnalysisJson.Options));
        foreach (var failure in observations.Where(item => item.ExpectedPresent != item.ActualPresent))
        {
            Console.WriteLine(
                $"P14_FACT_FAILURE={failure.Id};expected={failure.ExpectedPresent};actual={failure.ActualPresent}");
        }

        Assert.True(report.Precision >= targets.MinimumPrecision,
            $"Precision {report.Precision:P2} is below {targets.MinimumPrecision:P2}.");
        Assert.True(report.Recall >= targets.MinimumRecall,
            $"Recall {report.Recall:P2} is below {targets.MinimumRecall:P2}.");
        Assert.True(report.FalsePositiveRate <= targets.MaximumFalsePositiveRate,
            $"False-positive rate {report.FalsePositiveRate:P2} exceeds {targets.MaximumFalsePositiveRate:P2}.");
        Assert.True(report.FalseNegativeRate <= targets.MaximumFalseNegativeRate,
            $"False-negative rate {report.FalseNegativeRate:P2} exceeds {targets.MaximumFalseNegativeRate:P2}.");
        Assert.True(report.TaskDuplicationRate <= targets.MaximumTaskDuplicationRate,
            $"Task duplication rate {report.TaskDuplicationRate:P2} exceeds {targets.MaximumTaskDuplicationRate:P2}.");
        Assert.True(report.TaskReconciliationAccuracy >= targets.MinimumTaskReconciliationAccuracy,
            $"Task reconciliation accuracy {report.TaskReconciliationAccuracy:P2} is below {targets.MinimumTaskReconciliationAccuracy:P2}.");
        Assert.True(report.FastPathP95Milliseconds < targets.MaximumFastPathP95Milliseconds,
            $"Fast-path p95 {report.FastPathP95Milliseconds:F2} ms must be under " +
            $"{targets.MaximumFastPathP95Milliseconds:F2} ms.");
    }

    private static bool IsPresent(
        QualityFact fact,
        InitialRepositoryAnalysisResult initial,
        RetrievalContext context) => fact.Kind switch
        {
            "technology" => initial.Technologies.Any(item =>
                string.Equals(item.Technology.ToString(), fact.Name, StringComparison.Ordinal)),
            "artifact" => initial.Graph.Artifacts.Any(item =>
                string.Equals(item.Name, fact.Name, StringComparison.Ordinal) &&
                string.Equals(item.Kind.ToString(), fact.ArtifactKind, StringComparison.Ordinal)),
            "dependency" => HasDependency(initial.Graph, fact),
            "matchedContract" => initial.Graph.ContractPairs.Any(pair => pair.Status == ContractPairStatus.Matched),
            "contractField" => HasContractField(initial.Graph, fact),
            "permission" => initial.Graph.Contracts.Any(contract =>
                contract.Permissions.Contains(fact.Name!, StringComparer.Ordinal)),
            "retrievedArtifact" => context.Artifacts.Any(item =>
                string.Equals(item.Name, fact.Name, StringComparison.Ordinal)),
            "contractMismatch" => initial.Graph.ContractMismatches.Count > 0,
            _ => throw new InvalidOperationException($"Unknown quality fact kind '{fact.Kind}'.")
        };

    private static bool HasDependency(RepositoryKnowledgeGraph graph, QualityFact fact)
    {
        var sources = graph.Artifacts.Where(item => item.Name == fact.Source).Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
        var targets = graph.Artifacts.Where(item => item.Name == fact.Target).Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
        return graph.Dependencies.Any(item =>
            sources.Contains(item.SourceArtifactId) &&
            targets.Contains(item.Target) &&
            string.Equals(item.Kind.ToString(), fact.DependencyKind, StringComparison.Ordinal));
    }

    private static bool HasContractField(RepositoryKnowledgeGraph graph, QualityFact fact)
    {
        if (!Enum.TryParse<ContractDirection>(fact.Direction, out var direction))
        {
            throw new InvalidOperationException($"Unknown contract direction '{fact.Direction}'.");
        }

        return graph.Contracts.Where(contract => contract.Direction == direction)
            .SelectMany(contract => string.Equals(fact.FieldSection, "request", StringComparison.Ordinal)
                ? contract.RequestFields
                : contract.ResponseFields)
            .Any(field => string.Equals(field.Name, fact.Name, StringComparison.Ordinal));
    }

    private static ClassificationMetrics Classify(IReadOnlyCollection<FactObservation> observations)
    {
        var truePositive = observations.Count(item => item.ExpectedPresent && item.ActualPresent);
        var falsePositive = observations.Count(item => !item.ExpectedPresent && item.ActualPresent);
        var trueNegative = observations.Count(item => !item.ExpectedPresent && !item.ActualPresent);
        var falseNegative = observations.Count(item => item.ExpectedPresent && !item.ActualPresent);
        return new ClassificationMetrics(
            Ratio(truePositive, truePositive + falsePositive),
            Ratio(truePositive, truePositive + falseNegative),
            Ratio(falsePositive, falsePositive + trueNegative),
            Ratio(falseNegative, falseNegative + truePositive),
            truePositive,
            falsePositive,
            trueNegative,
            falseNegative);
    }

    private static TaskQualityMetrics MeasureTaskReconciliation()
    {
        var service = new TaskReconciliationService();
        var now = DateTimeOffset.Parse("2026-08-23T00:00:00Z");
        var proposal = Proposal();
        var created = service.Reconcile(proposal, [], now);
        var original = created.Mutations.Single().After;
        var cases = new[]
        {
            (Actual: created.Action, Expected: TaskReconciliationAction.Create),
            (Actual: service.Reconcile(proposal, [original], now.AddMinutes(1)).Action,
                Expected: TaskReconciliationAction.Ignore),
            (Actual: service.Reconcile(
                    proposal with { Requirements = ["accept categoryId", "persist categoryId"] },
                    [original],
                    now.AddMinutes(2)).Action,
                Expected: TaskReconciliationAction.Update),
            (Actual: service.Reconcile(
                    proposal,
                    [original, original with { Id = "duplicate-task", CreatedAt = now.AddSeconds(1) }],
                    now.AddMinutes(3)).Action,
                Expected: TaskReconciliationAction.Merge),
            (Actual: service.Reconcile(
                    proposal with { ChangeState = SourceChangeState.Reverted },
                    [original],
                    now.AddMinutes(4)).Action,
                Expected: TaskReconciliationAction.Close),
            (Actual: service.Reconcile(
                    proposal,
                    [original with { Status = SourceAwareTaskStatus.Cancelled }],
                    now.AddMinutes(5)).Action,
                Expected: TaskReconciliationAction.Reopen),
            (Actual: service.Reconcile(
                    proposal with { ChangeState = SourceChangeState.Reverted },
                    [original with { Status = SourceAwareTaskStatus.Completed }],
                    now.AddMinutes(6)).Action,
                Expected: TaskReconciliationAction.Ignore)
        };
        var accuracy = Ratio(cases.Count(item => item.Actual == item.Expected), cases.Length);

        var tasks = new Dictionary<string, SourceAwareEngineeringTask>(StringComparer.Ordinal);
        for (var iteration = 0; iteration < 10; iteration++)
        {
            var decision = service.Reconcile(proposal, tasks.Values.ToArray(), now.AddSeconds(iteration));
            foreach (var mutation in decision.Mutations)
            {
                tasks[mutation.After.Id] = mutation.After;
            }
        }

        var active = tasks.Values.Where(task => task.Status != SourceAwareTaskStatus.Cancelled).ToArray();
        var duplicateCount = active.GroupBy(task => task.CorrelationKey, StringComparer.Ordinal)
            .Sum(group => Math.Max(0, group.Count() - 1));
        return new TaskQualityMetrics(Ratio(duplicateCount, active.Length), accuracy);
    }

    private static async Task<double> MeasureFastPathP95Async(int maximumChangedFiles)
    {
        var paths = Enumerable.Range(1, maximumChangedFiles)
            .Select(index => $"src/features/feature-{index}.ts")
            .ToArray();
        var changes = paths.Select(path => new SourceFileChange(
                path,
                "export const value = 1",
                "export async function save() { return api.post('/api/items', {}) }"))
            .ToArray();
        var files = changes.Select(change => new RepositoryFile(change.Path, change.Path, change.After!)).ToArray();
        var service = new IncrementalMonitoringService(
            new StaticChangeSource(new IncrementalChangeSet(changes, files)),
            new InMemoryIncrementalDeliveryRegistry(),
            new InMemoryDeepReasoningQueue(),
            [new VueAnalyzer(), new AspNetAnalyzer(), new MartenAnalyzer()],
            TimeProvider.System);
        var graph = new RepositoryKnowledgeGraphAssembler().Build(RepositoryId, []);
        var samples = new List<double>();

        for (var iteration = 0; iteration < 35; iteration++)
        {
            var result = await service.ProcessAsync(
                IncrementalWorkItem($"benchmark-{iteration}", paths),
                graph,
                TestContext.Current.CancellationToken);
            if (iteration >= 5)
            {
                samples.Add(result.Elapsed.TotalMilliseconds);
            }
        }

        samples.Sort();
        return samples[(int)Math.Ceiling(samples.Count * 0.95) - 1];
    }

    private static StructuredTaskProposal Proposal() => new(
        "proposal-1",
        ProjectId,
        RepositoryId,
        "create-product-category-contract",
        "mismatch-1",
        "Align product category contract",
        "Keep the source-backed frontend and backend request contracts aligned.",
        VietAIS.TCFlow.Analyzers.Governance.PlanTargetComponent.Backend,
        EvidenceLevel.Inferred,
        0.95m,
        ["artifact-1"],
        ["evidence-1"],
        ["change-1"],
        ["accept categoryId"],
        SourceChangeState.Active,
        TaskProposalDisposition.Create);

    private static RepositoryAnalysisWorkItem InitialWorkItem() => new(
        "p14-initial",
        ProjectId,
        RepositoryId,
        "p14-initial",
        "fixture",
        RepositoryAnalysisKind.FullScan,
        RepositoryAnalysisTrigger.InitialScan,
        null,
        null,
        "refs/heads/main",
        null,
        RequiresContentFetch: false,
        [],
        DateTimeOffset.Parse("2026-08-23T00:00:00Z"),
        RepositoryAnalysisRequesterKind.System,
        null);

    private static RepositoryAnalysisWorkItem IncrementalWorkItem(string id, IReadOnlyList<string> paths) => new(
        id,
        ProjectId,
        RepositoryId,
        $"delivery-{id}",
        "fixture",
        RepositoryAnalysisKind.Incremental,
        RepositoryAnalysisTrigger.Push,
        "before",
        "after",
        "refs/heads/main",
        null,
        RequiresContentFetch: false,
        paths.Select(path => new RepositoryChangedPath(path, ChangeKind.Modified)).ToArray(),
        DateTimeOffset.Parse("2026-08-23T00:00:00Z"),
        RepositoryAnalysisRequesterKind.System,
        null);

    private static async Task<IReadOnlyList<RepositoryFile>> DiscoverFixtureAsync() =>
        await new FileDiscovery().DiscoverAsync(
            Path.Combine(RepositoryRoot, "samples", "knowledge-graph-full-application"),
            new FileDiscoveryOptions(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".vue",
                ".ts",
                ".cs"
            }),
            TestContext.Current.CancellationToken);

    private static async Task<QualityTargets> LoadTargetsAsync() =>
        JsonSerializer.Deserialize<QualityTargets>(
            await File.ReadAllTextAsync(
                Path.Combine(
                    RepositoryRoot,
                    "samples",
                    "end-to-end-acceptance",
                    "expected",
                    "quality-targets.json"),
                TestContext.Current.CancellationToken),
            AnalysisJson.Options)
        ?? throw new InvalidOperationException("P14 quality targets are invalid.");

    private static double Ratio(int numerator, int denominator) =>
        denominator == 0 ? 0d : (double)numerator / denominator;

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
    private const string RepositoryId = "p14-fixture";

    private sealed record QualityTargets(
        double MinimumPrecision,
        double MinimumRecall,
        double MaximumFalsePositiveRate,
        double MaximumFalseNegativeRate,
        double MaximumTaskDuplicationRate,
        double MinimumTaskReconciliationAccuracy,
        double MaximumFastPathP95Milliseconds,
        int MaximumChangedFiles,
        IReadOnlyList<QualityFact> Facts);

    private sealed record QualityFact(
        string Id,
        string Kind,
        bool ExpectedPresent,
        string? Name = null,
        string? ArtifactKind = null,
        string? Source = null,
        string? Target = null,
        string? DependencyKind = null,
        string? Direction = null,
        string? FieldSection = null);

    private sealed record FactObservation(string Id, bool ExpectedPresent, bool ActualPresent);

    private sealed record ClassificationMetrics(
        double Precision,
        double Recall,
        double FalsePositiveRate,
        double FalseNegativeRate,
        int TruePositive,
        int FalsePositive,
        int TrueNegative,
        int FalseNegative);

    private sealed record TaskQualityMetrics(double DuplicationRate, double Accuracy);

    private sealed record QualityBenchmarkReport(
        double Precision,
        double Recall,
        double FalsePositiveRate,
        double FalseNegativeRate,
        double TaskDuplicationRate,
        double TaskReconciliationAccuracy,
        double FastPathP95Milliseconds,
        int TruePositive,
        int FalsePositive,
        int TrueNegative,
        int FalseNegative,
        int LabeledFacts,
        DateTimeOffset MeasuredAt);

    private sealed class StaticSnapshotSource(RepositorySnapshot snapshot) : IRepositorySnapshotSource
    {
        public Task<RepositorySnapshot> LoadAsync(
            RepositoryAnalysisWorkItem workItem,
            CancellationToken cancellationToken = default) => Task.FromResult(snapshot);
    }

    private sealed class StaticChangeSource(IncrementalChangeSet changeSet) : IIncrementalChangeSource
    {
        public Task<IncrementalChangeSet> LoadAsync(
            RepositoryAnalysisWorkItem workItem,
            CancellationToken cancellationToken = default) => Task.FromResult(changeSet);
    }
}
