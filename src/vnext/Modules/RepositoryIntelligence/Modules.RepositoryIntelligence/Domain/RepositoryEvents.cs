using VietAIS.TCFlow.Modules.RepositoryIntelligence.Contracts.Commands;

namespace VietAIS.TCFlow.Modules.RepositoryIntelligence.Domain;

public sealed record AnalysisStarted(Guid AnalysisRunId, Guid ProjectId, string RepositoryId, string CommitSha, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record ArtifactObserved(Guid AnalysisRunId, string Path, SourceFactKind Kind, string Symbol, string? Details, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record SourceChangeDetected(Guid AnalysisRunId, string ChangeKey, string Path, string ChangeType, string Summary, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record EvidenceRecorded(Guid AnalysisRunId, string EvidenceKey, string SourcePath, string Claim, string Confidence, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record AnalysisCompleted(Guid AnalysisRunId, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
