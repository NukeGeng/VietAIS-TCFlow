namespace VietAIS.TCFlow.Modules.EventStorming.Contracts.Commands;

public sealed record ConnectNodes(Guid BoardId, long ExpectedVersion, Guid FromNodeId, Guid ToNodeId, string Relationship, string ActorId, string CorrelationId, string? CausationId = null);
