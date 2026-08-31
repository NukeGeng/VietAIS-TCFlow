namespace VietAIS.TCFlow.Modules.Architecture.Contracts.Commands;

public sealed record RecordArchitectureDrift(Guid ModelId, long ExpectedVersion, string DriftKey, string Summary, string Evidence, string ActorId, string CorrelationId, string? CausationId = null);
