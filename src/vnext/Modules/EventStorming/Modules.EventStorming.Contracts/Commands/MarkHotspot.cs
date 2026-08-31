namespace VietAIS.TCFlow.Modules.EventStorming.Contracts.Commands;

public sealed record MarkHotspot(Guid BoardId, long ExpectedVersion, Guid NodeId, string Reason, string ActorId, string CorrelationId, string? CausationId = null);
