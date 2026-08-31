namespace VietAIS.TCFlow.Modules.Architecture.Domain;

public sealed class ArchitectureModel
{
    private readonly HashSet<Guid> _modules = [];
    private readonly HashSet<Guid> _entities = [];
    private readonly HashSet<string> _moduleRelationships = [];
    private readonly HashSet<string> _dataRelationships = [];
    private readonly HashSet<string> _drifts = [];
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    public void Apply(ArchitectureModelCreated e) { Id = e.ModelId; ProjectId = e.ProjectId; Name = e.Name; }
    public void Apply(ArchitectureModuleAdded e) => _modules.Add(e.ModuleId);
    public void Apply(ArchitectureModulesConnected e) => _moduleRelationships.Add(Key(e.FromModuleId, e.ToModuleId, e.Relationship));
    public void Apply(ArchitectureEntityAdded e) => _entities.Add(e.EntityId);
    public void Apply(ArchitectureDataRelationshipAdded e) => _dataRelationships.Add(Key(e.FromEntityId, e.ToEntityId, e.Relationship));
    public void Apply(ArchitectureDriftRecorded e) => _drifts.Add(e.DriftKey);

    public ArchitectureModuleAdded AddModule(Guid id, string name, string? description, string actor, string correlation, DateTimeOffset now)
    {
        Identity(actor, correlation); Empty(id); if (!_modules.Add(id)) throw new InvalidOperationException("The module already exists."); _modules.Remove(id);
        return new(Id, id, Text(name, 2, 200, nameof(name)), Optional(description), actor.Trim(), correlation.Trim(), now);
    }
    public ArchitectureModulesConnected ConnectModules(Guid from, Guid to, string relationship, string actor, string correlation, DateTimeOffset now)
    {
        Identity(actor, correlation); if (from == Guid.Empty || to == Guid.Empty || from == to || !_modules.Contains(from) || !_modules.Contains(to)) throw new InvalidOperationException("Both distinct modules must exist before connecting them.");
        relationship = Text(relationship, 2, 120, nameof(relationship)); var key = Key(from, to, relationship); if (!_moduleRelationships.Add(key)) throw new InvalidOperationException("The module relationship already exists."); _moduleRelationships.Remove(key);
        return new(Id, from, to, relationship, actor.Trim(), correlation.Trim(), now);
    }
    public ArchitectureEntityAdded AddEntity(Guid id, string name, string? description, string actor, string correlation, DateTimeOffset now)
    {
        Identity(actor, correlation); Empty(id); if (!_entities.Add(id)) throw new InvalidOperationException("The data entity already exists."); _entities.Remove(id);
        return new(Id, id, Text(name, 2, 200, nameof(name)), Optional(description), actor.Trim(), correlation.Trim(), now);
    }
    public ArchitectureDataRelationshipAdded AddDataRelationship(Guid from, Guid to, string relationship, string actor, string correlation, DateTimeOffset now)
    {
        Identity(actor, correlation); if (from == Guid.Empty || to == Guid.Empty || from == to || !_entities.Contains(from) || !_entities.Contains(to)) throw new InvalidOperationException("Both distinct entities must exist before connecting them.");
        relationship = Text(relationship, 2, 120, nameof(relationship)); var key = Key(from, to, relationship); if (!_dataRelationships.Add(key)) throw new InvalidOperationException("The data relationship already exists."); _dataRelationships.Remove(key);
        return new(Id, from, to, relationship, actor.Trim(), correlation.Trim(), now);
    }
    public ArchitectureDriftRecorded RecordDrift(string key, string summary, string evidence, string actor, string correlation, DateTimeOffset now)
    {
        Identity(actor, correlation); key = Text(key, 2, 300, nameof(key)); if (!_drifts.Add(key)) throw new InvalidOperationException("This architecture drift is already recorded."); _drifts.Remove(key);
        return new(Id, key, Text(summary, 2, 1000, nameof(summary)), Text(evidence, 2, 4000, nameof(evidence)), actor.Trim(), correlation.Trim(), now);
    }

    private static string Key(Guid from, Guid to, string relationship) => $"{from:N}:{to:N}:{relationship}";
    private static void Empty(Guid id) => ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);
    private static void Identity(string actor, string correlation) { ArgumentException.ThrowIfNullOrWhiteSpace(actor); ArgumentException.ThrowIfNullOrWhiteSpace(correlation); }
    private static string Text(string value, int min, int max, string name) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length < min || v.Length > max) throw new ArgumentException($"Value must contain between {min} and {max} characters.", name); return v; }
    private static string? Optional(string? value, int max = 2000) { if (value is null) return null; var v = value.Trim(); if (v.Length > max) throw new ArgumentException($"Value cannot exceed {max} characters.", nameof(value)); return v.Length == 0 ? null : v; }
}
