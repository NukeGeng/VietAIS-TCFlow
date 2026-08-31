namespace VietAIS.TCFlow.Modules.Architecture.Contracts.Commands;

public sealed record ConnectModules(Guid ModelId, long ExpectedVersion, Guid FromModuleId, Guid ToModuleId, string Relationship, string ActorId, string CorrelationId, string? CausationId = null);
