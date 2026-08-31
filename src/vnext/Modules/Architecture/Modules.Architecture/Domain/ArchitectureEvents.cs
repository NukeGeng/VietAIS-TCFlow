namespace VietAIS.TCFlow.Modules.Architecture.Domain;

public sealed record ArchitectureModelCreated(Guid ModelId, Guid ProjectId, string Name, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record ArchitectureModuleAdded(Guid ModelId, Guid ModuleId, string Name, string? Description, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record ArchitectureModulesConnected(Guid ModelId, Guid FromModuleId, Guid ToModuleId, string Relationship, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record ArchitectureEntityAdded(Guid ModelId, Guid EntityId, string Name, string? Description, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record ArchitectureDataRelationshipAdded(Guid ModelId, Guid FromEntityId, Guid ToEntityId, string Relationship, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record ArchitectureDriftRecorded(Guid ModelId, string DriftKey, string Summary, string Evidence, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
