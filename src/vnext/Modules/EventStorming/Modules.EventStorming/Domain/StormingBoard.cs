using VietAIS.TCFlow.Modules.EventStorming.Contracts.Commands;

namespace VietAIS.TCFlow.Modules.EventStorming.Domain;

public sealed class StormingBoard
{
    private readonly List<Guid> _orderedNodes = [];
    private readonly HashSet<Guid> _nodeIds = [];
    private readonly HashSet<string> _connections = [];
    private readonly HashSet<Guid> _hotspots = [];

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    public void Apply(BoardCreated e) { Id = e.BoardId; ProjectId = e.ProjectId; Name = e.Name; }
    public void Apply(StormingNodeAdded e) { _nodeIds.Add(e.NodeId); _orderedNodes.Add(e.NodeId); }
    public void Apply(StormingNodesConnected e) => _connections.Add(Key(e.FromNodeId, e.ToNodeId, e.Relationship));
    public void Apply(StormingHotspotMarked e) => _hotspots.Add(e.NodeId);
    public void Apply(StormingNodeReordered e)
    {
        _orderedNodes.Remove(e.NodeId);
        _orderedNodes.Insert(Math.Clamp(e.Position, 0, _orderedNodes.Count), e.NodeId);
    }

    public StormingNodeAdded AddNode(Guid nodeId, StormingNodeType type, string label, string? description, string actorId, string correlationId, DateTimeOffset now)
    {
        EnsureIdentity(actorId, correlationId);
        ArgumentOutOfRangeException.ThrowIfEqual(nodeId, Guid.Empty);
        if (!_nodeIds.Add(nodeId)) throw new InvalidOperationException("The node already exists on this board.");
        _nodeIds.Remove(nodeId);
        return new(Id, nodeId, type, Normalize(label, 2, 240, nameof(label)), NormalizeOptional(description, 2000), actorId.Trim(), correlationId.Trim(), now);
    }

    public StormingNodesConnected Connect(Guid from, Guid to, string relationship, string actorId, string correlationId, DateTimeOffset now)
    {
        EnsureIdentity(actorId, correlationId);
        if (from == Guid.Empty || to == Guid.Empty || from == to || !_nodeIds.Contains(from) || !_nodeIds.Contains(to)) throw new InvalidOperationException("Both distinct nodes must exist before connecting them.");
        relationship = Normalize(relationship, 2, 120, nameof(relationship));
        if (!_connections.Add(Key(from, to, relationship))) throw new InvalidOperationException("The node connection already exists.");
        _connections.Remove(Key(from, to, relationship));
        return new(Id, from, to, relationship, actorId.Trim(), correlationId.Trim(), now);
    }

    public StormingHotspotMarked MarkHotspot(Guid nodeId, string reason, string actorId, string correlationId, DateTimeOffset now)
    {
        EnsureIdentity(actorId, correlationId);
        if (!_nodeIds.Contains(nodeId)) throw new KeyNotFoundException("The hotspot node does not exist on this board.");
        if (!_hotspots.Add(nodeId)) throw new InvalidOperationException("The node is already marked as a hotspot.");
        _hotspots.Remove(nodeId);
        return new(Id, nodeId, Normalize(reason, 2, 1000, nameof(reason)), actorId.Trim(), correlationId.Trim(), now);
    }

    public StormingNodeReordered Reorder(Guid nodeId, int position, string actorId, string correlationId, DateTimeOffset now)
    {
        EnsureIdentity(actorId, correlationId);
        if (!_nodeIds.Contains(nodeId)) throw new KeyNotFoundException("The node does not exist on this board.");
        if (position < 0 || position >= _orderedNodes.Count) throw new ArgumentOutOfRangeException(nameof(position));
        return new(Id, nodeId, position, actorId.Trim(), correlationId.Trim(), now);
    }

    private static string Key(Guid from, Guid to, string relationship) => $"{from:N}:{to:N}:{relationship}";
    private static void EnsureIdentity(string actorId, string correlationId) { ArgumentException.ThrowIfNullOrWhiteSpace(actorId); ArgumentException.ThrowIfNullOrWhiteSpace(correlationId); }
    private static string Normalize(string value, int min, int max, string name) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length < min || v.Length > max) throw new ArgumentException($"Value must contain between {min} and {max} characters.", name); return v; }
    private static string? NormalizeOptional(string? value, int max) { if (value is null) return null; var v = value.Trim(); if (v.Length > max) throw new ArgumentException($"Value cannot exceed {max} characters.", nameof(value)); return v.Length == 0 ? null : v; }
}
