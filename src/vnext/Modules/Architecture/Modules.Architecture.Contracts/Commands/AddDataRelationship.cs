namespace VietAIS.TCFlow.Modules.Architecture.Contracts.Commands;

public sealed record AddDataRelationship(Guid ModelId, long ExpectedVersion, Guid FromEntityId, Guid ToEntityId, string Relationship, string ActorId, string CorrelationId, string? CausationId = null);
