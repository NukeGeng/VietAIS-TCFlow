using Marten.Events.Aggregation;
using VietAIS.TCFlow.Modules.Architecture.Contracts.Queries;
using VietAIS.TCFlow.Modules.Architecture.Domain;

namespace VietAIS.TCFlow.Modules.Architecture.Projections;

public static class ArchitectureProjectionNames
{
    public const string Current = "architecture-current";
    public const string Overview = "architecture-overview";
}

public sealed class ArchitectureCurrent
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public long Version { get; set; }
    public List<ArchitectureModuleView> Modules { get; set; } = [];
    public List<ArchitectureRelationshipView> ModuleRelationships { get; set; } = [];
    public List<ArchitectureModuleView> Entities { get; set; } = [];
    public List<ArchitectureRelationshipView> DataRelationships { get; set; } = [];
    public List<ArchitectureDriftView> Drifts { get; set; } = [];
    public DateTimeOffset LastChangedAtUtc { get; set; }
}

public sealed class ArchitectureCurrentProjection : SingleStreamProjection<ArchitectureCurrent, Guid>
{
    public ArchitectureCurrentProjection() => Name = ArchitectureProjectionNames.Current;
    public static ArchitectureCurrent Create(ArchitectureModelCreated e) => new() { Id = e.ModelId, ProjectId = e.ProjectId, Name = e.Name, Version = 1, LastChangedAtUtc = e.OccurredAtUtc };
    public static void Apply(ArchitectureModuleAdded e, ArchitectureCurrent x) { x.Modules.Add(new(e.ModuleId, e.Name, e.Description)); Set(x, e.OccurredAtUtc); }
    public static void Apply(ArchitectureModulesConnected e, ArchitectureCurrent x) { x.ModuleRelationships.Add(new(e.FromModuleId, e.ToModuleId, e.Relationship)); Set(x, e.OccurredAtUtc); }
    public static void Apply(ArchitectureEntityAdded e, ArchitectureCurrent x) { x.Entities.Add(new(e.EntityId, e.Name, e.Description)); Set(x, e.OccurredAtUtc); }
    public static void Apply(ArchitectureDataRelationshipAdded e, ArchitectureCurrent x) { x.DataRelationships.Add(new(e.FromEntityId, e.ToEntityId, e.Relationship)); Set(x, e.OccurredAtUtc); }
    public static void Apply(ArchitectureDriftRecorded e, ArchitectureCurrent x) { x.Drifts.Add(new(e.DriftKey, e.Summary, e.Evidence)); Set(x, e.OccurredAtUtc); }
    private static void Set(ArchitectureCurrent x, DateTimeOffset at) { x.Version++; x.LastChangedAtUtc = at; }
}

public sealed class ArchitectureOverview
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ModuleCount { get; set; }
    public int EntityCount { get; set; }
    public int DriftCount { get; set; }
    public DateTimeOffset LastChangedAtUtc { get; set; }
}

public sealed class ArchitectureOverviewProjection : SingleStreamProjection<ArchitectureOverview, Guid>
{
    public ArchitectureOverviewProjection() => Name = ArchitectureProjectionNames.Overview;
    public static ArchitectureOverview Create(ArchitectureModelCreated e) => new() { Id = e.ModelId, ProjectId = e.ProjectId, Name = e.Name, LastChangedAtUtc = e.OccurredAtUtc };
    public static void Apply(ArchitectureModuleAdded e, ArchitectureOverview x) { x.ModuleCount++; x.LastChangedAtUtc = e.OccurredAtUtc; }
    public static void Apply(ArchitectureEntityAdded e, ArchitectureOverview x) { x.EntityCount++; x.LastChangedAtUtc = e.OccurredAtUtc; }
    public static void Apply(ArchitectureDriftRecorded e, ArchitectureOverview x) { x.DriftCount++; x.LastChangedAtUtc = e.OccurredAtUtc; }
    public static void Apply(ArchitectureModulesConnected e, ArchitectureOverview x) => x.LastChangedAtUtc = e.OccurredAtUtc;
    public static void Apply(ArchitectureDataRelationshipAdded e, ArchitectureOverview x) => x.LastChangedAtUtc = e.OccurredAtUtc;
}
