using VietAIS.TCFlow.Analyzers.Core;
using VietAIS.TCFlow.Analyzers.Governance;
using VietAIS.TCFlow.Analyzers.Knowledge;

namespace VietAIS.TCFlow.Analyzers.Monitoring;

public enum InitialRepositoryAnalysisStatus
{
    Completed,
    Unsupported
}

public sealed record RepositorySnapshot(
    string Revision,
    IReadOnlyList<RepositoryFile> Files);

public sealed record RepositoryTechnologySummary(
    TechnologyKind Technology,
    EvidenceLevel EvidenceLevel,
    int FileCount,
    IReadOnlyList<string> Reasons);

public sealed record InitialRepositoryAnalysisOptions(
    int MaximumFiles = 20_000,
    long MaximumTotalCharacters = 50_000_000);

public sealed record InitialRepositoryAnalysisResult(
    string RequestId,
    string SourceRevision,
    InitialRepositoryAnalysisStatus Status,
    IReadOnlyList<RepositoryTechnologySummary> Technologies,
    IReadOnlyList<AnalysisResult> Analyses,
    RepositoryKnowledgeGraph Graph,
    RepositoryConventionProfile Conventions,
    RepositoryAuthorityPolicy SuggestedAuthority,
    IReadOnlyList<AnalyzerDiagnostic> Diagnostics);

public interface IRepositorySnapshotSource
{
    Task<RepositorySnapshot> LoadAsync(
        RepositoryAnalysisWorkItem workItem,
        CancellationToken cancellationToken = default);
}

public sealed class InitialRepositoryAnalysisService(
    IRepositorySnapshotSource source,
    IReadOnlyCollection<IRepositoryAnalyzer> analyzers,
    InitialRepositoryAnalysisOptions? options = null,
    RepositoryKnowledgeGraphAssembler? graphAssembler = null,
    ConventionDetector? conventionDetector = null)
{
    private const int MaximumPathLength = 1000;
    private readonly InitialRepositoryAnalysisOptions _options = options ?? new InitialRepositoryAnalysisOptions();
    private readonly RepositoryKnowledgeGraphAssembler _graphAssembler = graphAssembler ?? new();
    private readonly ConventionDetector _conventionDetector = conventionDetector ?? new();

    public async Task<InitialRepositoryAnalysisResult> ProcessAsync(
        RepositoryAnalysisWorkItem workItem,
        long graphRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        ValidateWorkItem(workItem);
        ValidateOptions(graphRevision);
        EnsureUniqueAnalyzers();

        var snapshot = await source.LoadAsync(workItem, cancellationToken);
        var files = ValidateSnapshot(snapshot);
        var technologies = DetectTechnologies(files);
        var selectedAnalyzers = analyzers
            .Where(analyzer => AppliesToRepository(analyzer, files))
            .OrderBy(analyzer => analyzer.Name, StringComparer.Ordinal)
            .ToArray();
        var analyses = new List<AnalysisResult>(selectedAnalyzers.Length);
        foreach (var analyzer in selectedAnalyzers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await analyzer.AnalyzeAsync(files, cancellationToken);
            if (!string.Equals(result.Analyzer, analyzer.Name, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Analyzer '{analyzer.Name}' returned producer '{result.Analyzer}'.");
            }

            analyses.Add(result);
        }

        var graph = _graphAssembler.Build(workItem.RepositoryId, analyses, graphRevision);
        var conventions = _conventionDetector.Detect(graph);
        var authority = AuthorityPolicyDefaults.Suggest(workItem.ProjectId, graph);
        var diagnostics = analyses.SelectMany(analysis => analysis.Diagnostics).ToList();
        var status = HasRepositoryFacts(graph)
            ? InitialRepositoryAnalysisStatus.Completed
            : InitialRepositoryAnalysisStatus.Unsupported;
        if (status == InitialRepositoryAnalysisStatus.Unsupported)
        {
            diagnostics.Add(new AnalyzerDiagnostic(
                "ANALYSIS001",
                "The repository contains no source facts supported by the configured analyzers.",
                EvidenceLevel.Confirmed));
        }

        return new InitialRepositoryAnalysisResult(
            workItem.RequestId,
            snapshot.Revision.Trim(),
            status,
            technologies,
            analyses,
            graph,
            conventions,
            authority,
            diagnostics.OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Location?.Path, StringComparer.Ordinal)
                .ToArray());
    }

    private void ValidateOptions(long graphRevision)
    {
        if (graphRevision < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(graphRevision), "Knowledge revision must be positive.");
        }

        if (_options.MaximumFiles < 1 || _options.MaximumTotalCharacters < 1)
        {
            throw new InvalidOperationException("Initial analysis limits must be positive.");
        }
    }

    private void EnsureUniqueAnalyzers()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var analyzer in analyzers)
        {
            ArgumentNullException.ThrowIfNull(analyzer);
            if (string.IsNullOrWhiteSpace(analyzer.Name) || !names.Add(analyzer.Name))
            {
                throw new InvalidOperationException("Initial analyzers must have unique non-empty names.");
            }
        }
    }

    private IReadOnlyList<RepositoryFile> ValidateSnapshot(RepositorySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (string.IsNullOrWhiteSpace(snapshot.Revision))
        {
            throw new InvalidOperationException("Repository snapshot revision is required.");
        }

        ArgumentNullException.ThrowIfNull(snapshot.Files);
        if (snapshot.Files.Count > _options.MaximumFiles)
        {
            throw new InvalidOperationException(
                $"Repository snapshot exceeds the {_options.MaximumFiles} file analysis limit.");
        }

        var normalized = new Dictionary<string, RepositoryFile>(StringComparer.Ordinal);
        long totalCharacters = 0;
        foreach (var file in snapshot.Files)
        {
            ArgumentNullException.ThrowIfNull(file);
            var path = NormalizePath(file.RelativePath);
            totalCharacters = checked(totalCharacters + file.Content.Length);
            if (totalCharacters > _options.MaximumTotalCharacters)
            {
                throw new InvalidOperationException(
                    $"Repository snapshot exceeds the {_options.MaximumTotalCharacters} character analysis limit.");
            }

            if (!normalized.TryAdd(path, file with { RelativePath = path }))
            {
                throw new InvalidOperationException($"Repository snapshot contains duplicate path '{path}'.");
            }
        }

        return normalized.Values.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray();
    }

    private static string NormalizePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Repository snapshot paths are required.");
        }

        var path = value.Trim().Replace('\\', '/');
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (path.Length > MaximumPathLength ||
            path[0] == '/' ||
            (path.Length > 1 && path[1] == ':') ||
            segments.Length == 0 ||
            segments.Any(segment => segment is "." or ".."))
        {
            throw new InvalidOperationException(
                "Repository snapshot paths must be safe repository-relative paths of at most 1000 characters.");
        }

        return string.Join('/', segments);
    }

    private static bool AppliesToRepository(
        IRepositoryAnalyzer analyzer,
        IReadOnlyCollection<RepositoryFile> files) =>
        analyzer is IRepositoryAnalyzerApplicability applicability
            ? applicability.SupportsRepository(files)
            : files.Any(analyzer.Supports);

    private static IReadOnlyList<RepositoryTechnologySummary> DetectTechnologies(
        IReadOnlyCollection<RepositoryFile> files)
    {
        var detections = files.SelectMany(file => TechnologyDetector.Detect(file)
                .Where(detection => detection.Technology != TechnologyKind.Unknown)
                .Select(detection => new { File = file.RelativePath, Detection = detection }))
            .GroupBy(item => item.Detection.Technology)
            .OrderBy(group => group.Key)
            .Select(group => new RepositoryTechnologySummary(
                group.Key,
                group.Min(item => item.Detection.EvidenceLevel),
                group.Select(item => item.File).Distinct(StringComparer.Ordinal).Count(),
                group.Select(item => item.Detection.Reason)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray()))
            .ToArray();
        return detections.Length == 0
            ? [new RepositoryTechnologySummary(
                TechnologyKind.Unknown,
                EvidenceLevel.Inferred,
                files.Count,
                ["No supported deterministic technology signal was found."])]
            : detections;
    }

    private static bool HasRepositoryFacts(RepositoryKnowledgeGraph graph) =>
        graph.Artifacts.Count > 0 ||
        graph.Dependencies.Count > 0 ||
        graph.Evidence.Count > 0 ||
        graph.Capabilities.Count > 0 ||
        graph.Contracts.Count > 0;

    private static void ValidateWorkItem(RepositoryAnalysisWorkItem workItem)
    {
        if (workItem.Kind != RepositoryAnalysisKind.FullScan ||
            workItem.Trigger != RepositoryAnalysisTrigger.InitialScan ||
            workItem.ChangedPaths.Count != 0 ||
            workItem.RequiresContentFetch ||
            workItem.PullRequestNumber is not null)
        {
            throw new InvalidOperationException(
                "Initial repository analysis only accepts full scans without event change metadata.");
        }
    }
}
