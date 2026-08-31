namespace VietAIS.TCFlow.Modules.TaskFlow.Contracts.Commands;

public sealed record RejectReview(Guid TaskId, long ExpectedVersion, string Reason, string ActorId, string CorrelationId, string? CausationId = null);
