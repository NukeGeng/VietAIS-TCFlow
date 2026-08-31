namespace VietAIS.TCFlow.Modules.RepositoryIntelligence.Contracts.Commands;

public sealed record RecordEvidence(Guid AnalysisRunId, long ExpectedVersion, string EvidenceKey, string SourcePath, string Claim, string Confidence, string ActorId, string CorrelationId, string? CausationId = null);
