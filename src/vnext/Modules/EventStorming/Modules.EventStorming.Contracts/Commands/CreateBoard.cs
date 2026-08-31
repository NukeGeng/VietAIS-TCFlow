namespace VietAIS.TCFlow.Modules.EventStorming.Contracts.Commands;

public sealed record CreateBoard(Guid ProjectId, string Name, string ActorId, string CorrelationId, Guid? BoardId = null, string? CausationId = null);
