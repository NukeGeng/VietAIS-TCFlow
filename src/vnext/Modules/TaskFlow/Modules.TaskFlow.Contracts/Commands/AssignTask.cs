namespace VietAIS.TCFlow.Modules.TaskFlow.Contracts.Commands;

public sealed record AssignTask(Guid TaskId, long ExpectedVersion, string AssigneeId, string ActorId, string CorrelationId, string? CausationId = null);
