namespace VietAIS.TCFlow.Modules.Architecture.Contracts.Commands;

public sealed record AddDataEntity(Guid ModelId, long ExpectedVersion, Guid EntityId, string Name, string? Description, string ActorId, string CorrelationId, string? CausationId = null);
