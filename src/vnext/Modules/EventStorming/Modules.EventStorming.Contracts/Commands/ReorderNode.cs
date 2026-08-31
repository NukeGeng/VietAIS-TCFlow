namespace VietAIS.TCFlow.Modules.EventStorming.Contracts.Commands;

public sealed record ReorderNode(Guid BoardId, long ExpectedVersion, Guid NodeId, int Position, string ActorId, string CorrelationId, string? CausationId = null);
