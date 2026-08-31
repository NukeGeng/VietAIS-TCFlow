namespace VietAIS.TCFlow.Modules.RepositoryIntelligence.Contracts.Commands;

public sealed record StartAnalysis(Guid ProjectId, string RepositoryId, string CommitSha, string ActorId, string CorrelationId, Guid? AnalysisRunId = null, string? CausationId = null);
