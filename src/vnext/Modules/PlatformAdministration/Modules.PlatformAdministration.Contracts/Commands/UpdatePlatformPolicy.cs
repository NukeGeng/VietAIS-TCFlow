namespace VietAIS.TCFlow.Modules.PlatformAdministration.Contracts.Commands;

public sealed record UpdatePlatformPolicy(Guid PolicyId, long ExpectedVersion, bool AllowAiAnalysis, bool AllowAiTaskSuggestions, bool AllowAiTaskMutations, string ActorId, string CorrelationId, string? CausationId = null);
