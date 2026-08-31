namespace VietAIS.TCFlow.Modules.TaskFlow.Contracts.Commands;

public sealed record RejectTask(Guid TaskId, long ExpectedVersion, string Reason, string ActorId, string CorrelationId, string? CausationId = null);
