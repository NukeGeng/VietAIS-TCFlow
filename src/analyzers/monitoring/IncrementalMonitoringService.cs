using System.Diagnostics;
using VietAIS.TCFlow.Analyzers.Contracts;
using VietAIS.TCFlow.Analyzers.Core;
using VietAIS.TCFlow.Analyzers.Knowledge;

namespace VietAIS.TCFlow.Analyzers.Monitoring;

public sealed class IncrementalMonitoringService(
    IIncrementalChangeSource changeSource,
    IIncrementalDeliveryRegistry deliveryRegistry,
    IDeepReasoningQueue deepReasoningQueue,
    IReadOnlyCollection<IRepositoryAnalyzer> analyzers,
    TimeProvider timeProvider,
    MeaningfulChangeFilter? changeFilter = null,
    RepositoryKnowledgeGraphAssembler? graphAssembler = null,
    KnowledgeRetriever? retriever = null)
{
    private const string MonitoringProducer = "incremental-monitoring-v1";
    private readonly MeaningfulChangeFilter _changeFilter = changeFilter ?? new MeaningfulChangeFilter();
    private readonly RepositoryKnowledgeGraphAssembler _graphAssembler = graphAssembler ?? new();
    private readonly KnowledgeRetriever _retriever = retriever ?? new();

    public async Task<IncrementalMonitoringResult> ProcessAsync(
        RepositoryAnalysisWorkItem workItem,
        RepositoryKnowledgeGraph currentGraph,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        ArgumentNullException.ThrowIfNull(currentGraph);
        ValidateWorkItem(workItem, currentGraph);
        var started = Stopwatch.GetTimestamp();
        var deliveryKey = StableIdentity.Create(
            "incremental-delivery",
            workItem.SourceProvider,
            workItem.RepositoryId,
            workItem.CorrelationId);
        if (!await deliveryRegistry.TryBeginAsync(deliveryKey, cancellationToken))
        {
            return new IncrementalMonitoringResult(
                workItem.RequestId,
                IncrementalMonitoringStatus.Duplicate,
                currentGraph,
                [],
                [],
                null,
                Stopwatch.GetElapsedTime(started),
                "The source delivery was already accepted; no duplicate analysis or task work was created.");
        }

        try
        {
            var changeSet = await changeSource.LoadAsync(workItem, cancellationToken);
            ValidateChangeSet(workItem, changeSet);
            var filtered = changeSet.Changes
                .OrderBy(change => NormalizePath(change.Path), StringComparer.Ordinal)
                .Select(_changeFilter.Evaluate)
                .ToArray();
            var meaningful = filtered.Where(result => result.Decision == ChangeDecision.Meaningful).ToArray();
            if (meaningful.Length == 0)
            {
                await deliveryRegistry.MarkCompletedAsync(deliveryKey, cancellationToken);
                return new IncrementalMonitoringResult(
                    workItem.RequestId,
                    IncrementalMonitoringStatus.Ignored,
                    currentGraph,
                    filtered,
                    [],
                    null,
                    Stopwatch.GetElapsedTime(started),
                    "All changed files were cosmetic or non-behavioral; graph, AI, and tasks remain unchanged.");
            }

            var meaningfulPaths = meaningful.Select(result => result.Change.Path)
                .ToHashSet(StringComparer.Ordinal);
            var partialAnalyses = await AnalyzeChangedPathsAsync(
                meaningful,
                changeSet.AnalysisFiles,
                cancellationToken);
            var preview = partialAnalyses.Count == 0
                ? currentGraph
                : _graphAssembler.ApplyIncrementalPaths(currentGraph, partialAnalyses, meaningfulPaths);
            var impacts = GenerateImpacts(currentGraph, preview, meaningful);
            var monitoringAnalysis = new AnalysisResult(
                MonitoringProducer,
                "technology-neutral",
                [],
                [],
                [],
                [],
                [],
                meaningful.Select(result => result.Change).ToArray(),
                impacts,
                []);
            var replacements = partialAnalyses.Append(monitoringAnalysis).ToArray();
            var nextGraph = _graphAssembler.ApplyIncrementalPaths(currentGraph, replacements, meaningfulPaths);
            var revertedIds = DetectRevertedChanges(currentGraph, meaningful);
            var retrieval = _retriever.RetrieveForChanges(
                nextGraph,
                meaningful.Select(result => result.Change.Id).ToArray(),
                maxDepth: 2);
            var currentMismatchKeys = currentGraph.ContractMismatches.Select(SemanticMismatchKey)
                .ToHashSet();
            var mismatchIds = revertedIds.Count > 0
                ? []
                : retrieval.ContractMismatches.Where(mismatch =>
                        !currentMismatchKeys.Contains(SemanticMismatchKey(mismatch)))
                    .Select(mismatch => mismatch.Id)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
            var needsDeepReasoning = revertedIds.Count > 0 ||
                meaningful.Any(result => result.HasCrossLayerPotential) && mismatchIds.Length > 0;
            DeepReasoningWorkItem? deepReasoning = null;
            if (needsDeepReasoning)
            {
                var changeIds = meaningful.Select(result => result.Change.Id)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                deepReasoning = new DeepReasoningWorkItem(
                    StableIdentity.Create(
                        "deep-reasoning-job",
                        workItem.RepositoryId,
                        workItem.CorrelationId,
                        string.Join(',', changeIds)),
                    workItem.RequestId,
                    workItem.ProjectId,
                    workItem.RepositoryId,
                    workItem.CorrelationId,
                    nextGraph.Revision,
                    changeIds,
                    mismatchIds,
                    revertedIds,
                    timeProvider.GetUtcNow());
                await deepReasoningQueue.EnqueueAsync(deepReasoning, cancellationToken);
            }

            await deliveryRegistry.MarkCompletedAsync(deliveryKey, cancellationToken);
            return new IncrementalMonitoringResult(
                workItem.RequestId,
                deepReasoning is null
                    ? IncrementalMonitoringStatus.FastPathCompleted
                    : IncrementalMonitoringStatus.DeepReasoningQueued,
                nextGraph,
                filtered,
                impacts,
                deepReasoning,
                Stopwatch.GetElapsedTime(started),
                deepReasoning is null
                    ? "Deterministic incremental analysis completed without requiring AI reasoning."
                    : "Deterministic impact is available and targeted deep reasoning was queued.");
        }
        catch
        {
            await deliveryRegistry.MarkFailedAsync(deliveryKey, CancellationToken.None);
            throw;
        }
    }

    private async Task<IReadOnlyList<AnalysisResult>> AnalyzeChangedPathsAsync(
        IReadOnlyCollection<ChangeFilterResult> meaningful,
        IReadOnlyList<RepositoryFile> analysisFiles,
        CancellationToken cancellationToken)
    {
        var changedFiles = meaningful.Select(result => new RepositoryFile(
                result.Change.Path,
                result.Change.Path,
                string.Empty))
            .ToArray();
        var selected = analyzers.Where(analyzer => changedFiles.Any(analyzer.Supports))
            .OrderBy(analyzer => analyzer.Name, StringComparer.Ordinal)
            .ToArray();
        if (selected.Select(analyzer => analyzer.Name).Distinct(StringComparer.Ordinal).Count() != selected.Length ||
            selected.Any(analyzer => string.Equals(analyzer.Name, MonitoringProducer, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Incremental analyzers must have unique, non-reserved names.");
        }

        var results = new List<AnalysisResult>(selected.Length);
        foreach (var analyzer in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await analyzer.AnalyzeAsync(analysisFiles, cancellationToken));
        }

        return results;
    }

    private static IReadOnlyList<Impact> GenerateImpacts(
        RepositoryKnowledgeGraph current,
        RepositoryKnowledgeGraph preview,
        IReadOnlyCollection<ChangeFilterResult> changes)
    {
        var traversal = new KnowledgeGraphTraversal();
        var impacts = new List<Impact>();
        foreach (var result in changes)
        {
            var affected = preview.Artifacts
                .Where(artifact => string.Equals(NormalizePath(artifact.Path), result.Change.Path,
                    StringComparison.Ordinal))
                .ToArray();
            var indirect = false;
            if (affected.Length == 0 && result.Change.Kind == ChangeKind.Deleted)
            {
                var removedIds = current.Artifacts
                    .Where(artifact => string.Equals(NormalizePath(artifact.Path), result.Change.Path,
                        StringComparison.Ordinal))
                    .Select(artifact => artifact.Id)
                    .ToArray();
                if (removedIds.Length > 0)
                {
                    var neighbors = traversal.FindNeighborhood(current, removedIds, maxDepth: 1).ArtifactIds
                        .ToHashSet(StringComparer.Ordinal);
                    affected = preview.Artifacts.Where(artifact => neighbors.Contains(artifact.Id)).ToArray();
                    indirect = true;
                }
            }

            foreach (var artifact in affected)
            {
                var evidenceIds = artifact.EvidenceIds
                    .Where(id => preview.Evidence.Any(evidence => evidence.Id == id))
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                impacts.Add(new Impact(
                    StableIdentity.Create("incremental-impact", result.Change.Id, artifact.Id),
                    result.Change.Id,
                    artifact.Id,
                    result.HasCrossLayerPotential ? ImpactSeverity.High : ImpactSeverity.Low,
                    indirect
                        ? $"Deleting '{result.Change.Path}' can affect the connected artifact '{artifact.Name}'."
                        : $"The meaningful change in '{result.Change.Path}' affects artifact '{artifact.Name}'.",
                    indirect ? 0.8m : 0.95m,
                    EvidenceLevel.Inferred,
                    evidenceIds));
            }
        }

        return impacts.OrderBy(impact => impact.SourceChangeId, StringComparer.Ordinal)
            .ThenBy(impact => impact.AffectedArtifactId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> DetectRevertedChanges(
        RepositoryKnowledgeGraph current,
        IReadOnlyCollection<ChangeFilterResult> changes) => changes
        .SelectMany(change => current.Changes.Where(previous =>
            previous.IsMeaningful &&
            string.Equals(previous.Path, change.Change.Path, StringComparison.Ordinal) &&
            string.Equals(previous.BeforeHash, change.Change.AfterHash, StringComparison.Ordinal) &&
            string.Equals(previous.AfterHash, change.Change.BeforeHash, StringComparison.Ordinal)))
        .Select(change => change.Id)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static (ContractMismatchKind Kind, string Subject, string FrontendValue, string BackendValue)
        SemanticMismatchKey(ContractMismatch mismatch) =>
        (mismatch.Kind, mismatch.Subject, mismatch.FrontendValue, mismatch.BackendValue);

    private static void ValidateWorkItem(
        RepositoryAnalysisWorkItem workItem,
        RepositoryKnowledgeGraph currentGraph)
    {
        if (workItem.Kind != RepositoryAnalysisKind.Incremental ||
            workItem.Trigger == RepositoryAnalysisTrigger.InitialScan)
        {
            throw new InvalidOperationException("Incremental monitoring only accepts non-initial incremental work.");
        }

        if (!string.Equals(workItem.RepositoryId, currentGraph.RepositoryId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Analysis work and knowledge graph belong to different repositories.");
        }
    }

    private static void ValidateChangeSet(
        RepositoryAnalysisWorkItem workItem,
        IncrementalChangeSet changeSet)
    {
        ArgumentNullException.ThrowIfNull(changeSet);
        if (changeSet.Changes.Count == 0)
        {
            throw new InvalidOperationException("Incremental analysis requires at least one changed file.");
        }

        var changes = new Dictionary<string, SourceFileChange>(StringComparer.Ordinal);
        foreach (var change in changeSet.Changes)
        {
            ArgumentNullException.ThrowIfNull(change);
            var path = NormalizePath(change.Path);
            if (!changes.TryAdd(path, change))
            {
                throw new InvalidOperationException($"Changed file '{path}' was ingested more than once.");
            }
        }

        if (!workItem.RequiresContentFetch)
        {
            var expected = workItem.ChangedPaths.ToDictionary(path => NormalizePath(path.Path), StringComparer.Ordinal);
            if (expected.Count != changes.Count || expected.Any(item =>
                    !changes.TryGetValue(item.Key, out var actual) || actual.Kind != item.Value.Kind))
            {
                throw new InvalidOperationException(
                    "Ingested file contents do not match the changed paths from the source event.");
            }
        }

        var analysisPaths = changeSet.AnalysisFiles.Select(file => NormalizePath(file.RelativePath))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var change in changes.Values.Where(change => change.Kind != ChangeKind.Deleted))
        {
            if (!analysisPaths.Contains(NormalizePath(change.Path)) || change.After is null)
            {
                throw new InvalidOperationException(
                    $"Changed file '{change.Path}' requires after-content in the targeted analysis context.");
            }
        }
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Changed file paths are required.");
        }

        return path.Replace('\\', '/');
    }
}
