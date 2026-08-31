using VietAIS.TCFlow.Modules.RepositoryIntelligence.Contracts.Commands;

namespace VietAIS.TCFlow.Modules.RepositoryIntelligence.Contracts.Queries;

public sealed record SourceArtifactView(string Path, SourceFactKind Kind, string Symbol, string? Details);
public sealed record SourceChangeView(string ChangeKey, string Path, string ChangeType, string Summary);
public sealed record EvidenceView(string EvidenceKey, string SourcePath, string Claim, string Confidence);
public sealed record AnalysisView(Guid Id, Guid ProjectId, string RepositoryId, string CommitSha, bool Completed, long Version, IReadOnlyList<SourceArtifactView> Artifacts, IReadOnlyList<SourceChangeView> Changes, IReadOnlyList<EvidenceView> Evidence, DateTimeOffset LastChangedAtUtc);
