namespace VietAIS.TCFlow.Modules.Architecture.Contracts.Commands;

public sealed record CreateArchitectureModel(Guid ProjectId, string Name, string ActorId, string CorrelationId, Guid? ModelId = null, string? CausationId = null);
