namespace VietAIS.TCFlow.Modules.RepositoryIntelligence.Contracts.Commands;

public sealed record CompleteAnalysis(Guid AnalysisRunId, long ExpectedVersion, string ActorId, string CorrelationId, string? CausationId = null);
