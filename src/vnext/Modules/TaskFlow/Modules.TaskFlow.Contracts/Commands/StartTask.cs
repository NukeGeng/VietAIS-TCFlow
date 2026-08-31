namespace VietAIS.TCFlow.Modules.TaskFlow.Contracts.Commands;

public sealed record StartTask(Guid TaskId, long ExpectedVersion, string ActorId, string CorrelationId, string? CausationId = null);
