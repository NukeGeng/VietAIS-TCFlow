namespace VietAIS.TCFlow.Modules.Architecture.Contracts.Commands;

public sealed record AddModule(Guid ModelId, long ExpectedVersion, Guid ModuleId, string Name, string? Description, string ActorId, string CorrelationId, string? CausationId = null);
