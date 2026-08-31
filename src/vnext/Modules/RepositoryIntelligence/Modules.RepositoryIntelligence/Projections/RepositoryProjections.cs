using Marten.Events.Aggregation;
using VietAIS.TCFlow.Modules.RepositoryIntelligence.Contracts.Commands;
using VietAIS.TCFlow.Modules.RepositoryIntelligence.Contracts.Queries;
using VietAIS.TCFlow.Modules.RepositoryIntelligence.Domain;

namespace VietAIS.TCFlow.Modules.RepositoryIntelligence.Projections;

public static class RepositoryProjectionNames
{
    public const string Current = "repository-analysis-current";
    public const string KnowledgeGraph = "repository-knowledge-graph";
    public const string ImpactGraph = "repository-impact-graph";
}

public sealed class AnalysisCurrent
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string RepositoryId { get; set; } = string.Empty;
    public string CommitSha { get; set; } = string.Empty;
    public bool Completed { get; set; }
    public long Version { get; set; }
    public List<SourceArtifactView> Artifacts { get; set; } = [];
    public List<SourceChangeView> Changes { get; set; } = [];
    public List<EvidenceView> Evidence { get; set; } = [];
    public DateTimeOffset LastChangedAtUtc { get; set; }
}

public sealed class AnalysisCurrentProjection : SingleStreamProjection<AnalysisCurrent, Guid>
{
    public AnalysisCurrentProjection() => Name = RepositoryProjectionNames.Current;
    public static AnalysisCurrent Create(AnalysisStarted e) => new() { Id = e.AnalysisRunId, ProjectId = e.ProjectId, RepositoryId = e.RepositoryId, CommitSha = e.CommitSha, Version = 1, LastChangedAtUtc = e.OccurredAtUtc };
    public static void Apply(ArtifactObserved e, AnalysisCurrent x) { x.Artifacts.Add(new(e.Path, e.Kind, e.Symbol, e.Details)); Set(x, e.OccurredAtUtc); }
    public static void Apply(SourceChangeDetected e, AnalysisCurrent x) { x.Changes.Add(new(e.ChangeKey, e.Path, e.ChangeType, e.Summary)); Set(x, e.OccurredAtUtc); }
    public static void Apply(EvidenceRecorded e, AnalysisCurrent x) { x.Evidence.Add(new(e.EvidenceKey, e.SourcePath, e.Claim, e.Confidence)); Set(x, e.OccurredAtUtc); }
    public static void Apply(AnalysisCompleted e, AnalysisCurrent x) { x.Completed = true; Set(x, e.OccurredAtUtc); }
    private static void Set(AnalysisCurrent x, DateTimeOffset at) { x.Version++; x.LastChangedAtUtc = at; }
}

public sealed class KnowledgeGraph
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public int ArtifactCount { get; set; }
    public int EvidenceCount { get; set; }
    public DateTimeOffset LastChangedAtUtc { get; set; }
}

public sealed class KnowledgeGraphProjection : SingleStreamProjection<KnowledgeGraph, Guid>
{
    public KnowledgeGraphProjection() => Name = RepositoryProjectionNames.KnowledgeGraph;
    public static KnowledgeGraph Create(AnalysisStarted e) => new() { Id = e.AnalysisRunId, ProjectId = e.ProjectId, LastChangedAtUtc = e.OccurredAtUtc };
    public static void Apply(ArtifactObserved e, KnowledgeGraph x) { x.ArtifactCount++; x.LastChangedAtUtc = e.OccurredAtUtc; }
    public static void Apply(EvidenceRecorded e, KnowledgeGraph x) { x.EvidenceCount++; x.LastChangedAtUtc = e.OccurredAtUtc; }
    public static void Apply(SourceChangeDetected e, KnowledgeGraph x) => x.LastChangedAtUtc = e.OccurredAtUtc;
    public static void Apply(AnalysisCompleted e, KnowledgeGraph x) => x.LastChangedAtUtc = e.OccurredAtUtc;
}

public sealed class ImpactGraph
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public int ChangeCount { get; set; }
    public DateTimeOffset LastChangedAtUtc { get; set; }
}

public sealed class ImpactGraphProjection : SingleStreamProjection<ImpactGraph, Guid>
{
    public ImpactGraphProjection() => Name = RepositoryProjectionNames.ImpactGraph;
    public static ImpactGraph Create(AnalysisStarted e) => new() { Id = e.AnalysisRunId, ProjectId = e.ProjectId, LastChangedAtUtc = e.OccurredAtUtc };
    public static void Apply(SourceChangeDetected e, ImpactGraph x) { x.ChangeCount++; x.LastChangedAtUtc = e.OccurredAtUtc; }
    public static void Apply(ArtifactObserved e, ImpactGraph x) => x.LastChangedAtUtc = e.OccurredAtUtc;
    public static void Apply(EvidenceRecorded e, ImpactGraph x) => x.LastChangedAtUtc = e.OccurredAtUtc;
    public static void Apply(AnalysisCompleted e, ImpactGraph x) => x.LastChangedAtUtc = e.OccurredAtUtc;
}
