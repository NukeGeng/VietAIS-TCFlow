namespace VietAIS.TCFlow.Modules.Architecture.Contracts.Queries;

public sealed record ArchitectureModuleView(Guid Id, string Name, string? Description);
public sealed record ArchitectureRelationshipView(Guid FromId, Guid ToId, string Relationship);
public sealed record ArchitectureDriftView(string DriftKey, string Summary, string Evidence);
public sealed record ArchitectureView(Guid Id, Guid ProjectId, string Name, long Version, IReadOnlyList<ArchitectureModuleView> Modules, IReadOnlyList<ArchitectureRelationshipView> ModuleRelationships, IReadOnlyList<ArchitectureModuleView> Entities, IReadOnlyList<ArchitectureRelationshipView> DataRelationships, IReadOnlyList<ArchitectureDriftView> Drifts, DateTimeOffset LastChangedAtUtc);
