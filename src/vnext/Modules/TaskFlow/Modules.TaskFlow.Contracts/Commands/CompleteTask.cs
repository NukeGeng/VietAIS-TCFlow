namespace VietAIS.TCFlow.Modules.TaskFlow.Contracts.Commands;

public sealed record CompleteTask(Guid TaskId, long ExpectedVersion, string ActorId, string CorrelationId, string? CausationId = null);
