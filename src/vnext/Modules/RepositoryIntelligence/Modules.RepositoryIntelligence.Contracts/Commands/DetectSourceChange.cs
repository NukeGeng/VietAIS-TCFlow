namespace VietAIS.TCFlow.Modules.RepositoryIntelligence.Contracts.Commands;

public sealed record DetectSourceChange(Guid AnalysisRunId, long ExpectedVersion, string ChangeKey, string Path, string ChangeType, string Summary, string ActorId, string CorrelationId, string? CausationId = null);
