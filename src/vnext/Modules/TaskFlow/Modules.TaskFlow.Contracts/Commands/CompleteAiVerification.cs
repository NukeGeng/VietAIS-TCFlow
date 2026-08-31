namespace VietAIS.TCFlow.Modules.TaskFlow.Contracts.Commands;

public sealed record CompleteAiVerification(Guid TaskId, long ExpectedVersion, bool Passed, string Summary, string ActorId, string CorrelationId, string? CausationId = null);
