using System.Text.Json;
using System.Text.Json.Serialization;

namespace VietAIS.TCFlow.Analyzers.Core;

public enum EvidenceLevel
{
    Confirmed,
    Inferred,
    Proposed
}

public enum ArtifactKind
{
    VueComponent,
    TypeScriptInterface,
    ApiCall,
    FormField,
    ReactiveState,
    PiniaStore,
    VueRoute,
    PermissionCheck,
    Filter,
    Pagination,
    AspNetEndpoint,
    RequestDto,
    ResponseDto,
    Validator,
    Handler,
    Service,
    Interface,
    OpenApiOperation,
    MartenDocument,
    MartenSession,
    MartenOperation,
    Unknown
}

public enum DependencyKind
{
    Contains,
    Imports,
    Calls,
    Uses,
    NavigatesTo,
    Reads,
    Writes,
    Accepts,
    Returns,
    Validates,
    Authorizes,
    DelegatesTo,
    Produces,
    Deletes
}

public enum ContractDirection
{
    FrontendExpected,
    BackendActual
}

public enum ChangeKind
{
    Added,
    Modified,
    Deleted,
    Renamed
}

public enum ImpactSeverity
{
    None,
    Low,
    Medium,
    High,
    Critical
}

public sealed record SourceLocation(
    string Path,
    int StartLine,
    int EndLine,
    string? Symbol = null);

public sealed record Evidence(
    string Id,
    string Statement,
    EvidenceLevel Level,
    SourceLocation Location,
    string Extractor,
    decimal Confidence);

public sealed record Artifact(
    string Id,
    ArtifactKind Kind,
    string Technology,
    string Name,
    string Path,
    EvidenceLevel EvidenceLevel,
    IReadOnlyList<string> EvidenceIds,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record Dependency(
    string Id,
    string SourceArtifactId,
    string Target,
    DependencyKind Kind,
    EvidenceLevel EvidenceLevel,
    string EvidenceId);

public sealed record Capability(
    string Id,
    string Name,
    EvidenceLevel EvidenceLevel,
    IReadOnlyList<string> EvidenceIds,
    IReadOnlyList<string> ArtifactIds);

public sealed record ContractField(
    string Name,
    string Type,
    bool Required,
    EvidenceLevel EvidenceLevel,
    SourceLocation Location)
{
    public IReadOnlyList<string> Validations { get; init; } = [];
}

public sealed record Contract(
    string Id,
    ContractDirection Direction,
    string HttpMethod,
    string Route,
    EvidenceLevel EvidenceLevel,
    IReadOnlyList<ContractField> RequestFields,
    IReadOnlyList<ContractField> ResponseFields,
    IReadOnlyList<string> ErrorStates,
    bool HasPagination,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> EvidenceIds);

public sealed record SourceChange(
    string Id,
    string Path,
    ChangeKind Kind,
    string BeforeHash,
    string AfterHash,
    bool IsMeaningful,
    string Reason);

public sealed record Impact(
    string Id,
    string SourceChangeId,
    string AffectedArtifactId,
    ImpactSeverity Severity,
    string Reason,
    decimal Confidence,
    EvidenceLevel EvidenceLevel,
    IReadOnlyList<string> EvidenceIds);

public sealed record AnalyzerDiagnostic(
    string Code,
    string Message,
    EvidenceLevel Level,
    SourceLocation? Location = null);

public sealed record AnalysisResult(
    string Analyzer,
    string Technology,
    IReadOnlyList<Artifact> Artifacts,
    IReadOnlyList<Dependency> Dependencies,
    IReadOnlyList<Evidence> Evidence,
    IReadOnlyList<Capability> Capabilities,
    IReadOnlyList<Contract> Contracts,
    IReadOnlyList<SourceChange> Changes,
    IReadOnlyList<Impact> Impacts,
    IReadOnlyList<AnalyzerDiagnostic> Diagnostics)
{
    public static AnalysisResult Empty(string analyzer, string technology) =>
        new(analyzer, technology, [], [], [], [], [], [], [], []);
}

public sealed record RepositoryFile(string RelativePath, string FullPath, string Content);

public interface IRepositoryAnalyzer
{
    string Name { get; }

    bool Supports(RepositoryFile file);

    Task<AnalysisResult> AnalyzeAsync(
        IReadOnlyCollection<RepositoryFile> files,
        CancellationToken cancellationToken = default);
}

public static class AnalysisJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
