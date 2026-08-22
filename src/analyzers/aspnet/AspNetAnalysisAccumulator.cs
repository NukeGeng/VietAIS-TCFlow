using VietAIS.TCFlow.Analyzers.Core;

namespace VietAIS.TCFlow.Analyzers.AspNet;

internal sealed class AspNetAnalysisAccumulator(string analyzerName)
{
    private readonly Dictionary<string, Artifact> _artifacts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dependency> _dependencies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Evidence> _evidence = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Contract> _contracts = new(StringComparer.Ordinal);
    private readonly List<AnalyzerDiagnostic> _diagnostics = [];

    public string AddEvidence(
        RepositoryFile file,
        int index,
        string statement,
        EvidenceLevel level,
        string extractor,
        string? symbol = null)
    {
        var line = CSharpTextParsing.LineNumber(file.Content, index);
        var id = StableIdentity.Create("evidence", file.RelativePath, line.ToString(), statement, extractor);
        _evidence[id] = new Evidence(
            id,
            statement,
            level,
            new SourceLocation(file.RelativePath, line, line, symbol),
            extractor,
            level == EvidenceLevel.Confirmed ? 1m : 0.72m);
        return id;
    }

    public string AddArtifact(
        ArtifactKind kind,
        string name,
        RepositoryFile file,
        EvidenceLevel level,
        IEnumerable<string> evidenceIds,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        var id = StableIdentity.Create("artifact", "aspnet", kind.ToString(), file.RelativePath, name);
        var normalizedMetadata = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (metadata is not null)
        {
            foreach (var item in metadata)
            {
                normalizedMetadata[item.Key] = item.Value;
            }
        }

        _artifacts[id] = new Artifact(
            id,
            kind,
            "aspnet",
            name,
            file.RelativePath,
            level,
            evidenceIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            normalizedMetadata);
        return id;
    }

    public void AddDependency(
        string sourceArtifactId,
        string target,
        DependencyKind kind,
        EvidenceLevel level,
        string evidenceId)
    {
        var id = StableIdentity.Create("dependency", sourceArtifactId, target, kind.ToString());
        _dependencies[id] = new Dependency(id, sourceArtifactId, target, kind, level, evidenceId);
    }

    public void AddContract(Contract contract) => _contracts[contract.Id] = contract;

    public void AddDiagnostic(AnalyzerDiagnostic diagnostic) => _diagnostics.Add(diagnostic);

    public AnalysisResult Build() => new(
        analyzerName,
        "aspnet",
        _artifacts.Values.OrderBy(item => item.Path, StringComparer.Ordinal)
            .ThenBy(item => item.Kind)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .ToArray(),
        _dependencies.Values.OrderBy(item => item.SourceArtifactId, StringComparer.Ordinal)
            .ThenBy(item => item.Kind)
            .ThenBy(item => item.Target, StringComparer.Ordinal)
            .ToArray(),
        _evidence.Values.OrderBy(item => item.Location.Path, StringComparer.Ordinal)
            .ThenBy(item => item.Location.StartLine)
            .ThenBy(item => item.Statement, StringComparer.Ordinal)
            .ToArray(),
        [],
        _contracts.Values.OrderBy(item => item.Route, StringComparer.Ordinal)
            .ThenBy(item => item.HttpMethod, StringComparer.Ordinal)
            .ToArray(),
        [],
        [],
        _diagnostics.OrderBy(item => item.Location?.Path, StringComparer.Ordinal)
            .ThenBy(item => item.Location?.StartLine)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .ToArray());
}
