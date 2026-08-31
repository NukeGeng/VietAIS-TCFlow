using Marten.Events.Aggregation;
using VietAIS.TCFlow.Modules.EventStorming.Contracts.Commands;
using VietAIS.TCFlow.Modules.EventStorming.Contracts.Queries;
using VietAIS.TCFlow.Modules.EventStorming.Domain;

namespace VietAIS.TCFlow.Modules.EventStorming.Projections;

public static class StormingProjectionNames
{
    public const string BoardCanvas = "event-storming-board-canvas";
    public const string DomainEventCatalog = "event-storming-domain-event-catalog";
}

public sealed class BoardCanvas
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public long Version { get; set; }
    public List<StormingNodeView> Nodes { get; set; } = [];
    public List<StormingConnectionView> Connections { get; set; } = [];
    public DateTimeOffset LastChangedAtUtc { get; set; }
}

public sealed class BoardCanvasProjection : SingleStreamProjection<BoardCanvas, Guid>
{
    public BoardCanvasProjection() => Name = StormingProjectionNames.BoardCanvas;
    public static BoardCanvas Create(BoardCreated e) => new() { Id = e.BoardId, ProjectId = e.ProjectId, Name = e.Name, Version = 1, LastChangedAtUtc = e.OccurredAtUtc };
    public static void Apply(StormingNodeAdded e, BoardCanvas x) { x.Nodes.Add(new(e.NodeId, e.NodeType, e.Label, e.Description, false, x.Nodes.Count)); Set(x, e.OccurredAtUtc); }
    public static void Apply(StormingNodesConnected e, BoardCanvas x) { x.Connections.Add(new(e.FromNodeId, e.ToNodeId, e.Relationship)); Set(x, e.OccurredAtUtc); }
    public static void Apply(StormingHotspotMarked e, BoardCanvas x) { var i = x.Nodes.FindIndex(n => n.Id == e.NodeId); if (i >= 0) x.Nodes[i] = x.Nodes[i] with { IsHotspot = true }; Set(x, e.OccurredAtUtc); }
    public static void Apply(StormingNodeReordered e, BoardCanvas x)
    {
        var node = x.Nodes.Find(n => n.Id == e.NodeId);
        if (node is not null) { x.Nodes.Remove(node); x.Nodes.Insert(Math.Clamp(e.Position, 0, x.Nodes.Count), node); for (var i = 0; i < x.Nodes.Count; i++) x.Nodes[i] = x.Nodes[i] with { Position = i }; }
        Set(x, e.OccurredAtUtc);
    }
    private static void Set(BoardCanvas x, DateTimeOffset at) { x.Version++; x.LastChangedAtUtc = at; }
}

public sealed class DomainEventCatalog
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> DomainEvents { get; set; } = [];
    public DateTimeOffset LastChangedAtUtc { get; set; }
}

public sealed class DomainEventCatalogProjection : SingleStreamProjection<DomainEventCatalog, Guid>
{
    public DomainEventCatalogProjection() => Name = StormingProjectionNames.DomainEventCatalog;
    public static DomainEventCatalog Create(BoardCreated e) => new() { Id = e.BoardId, ProjectId = e.ProjectId, Name = e.Name, LastChangedAtUtc = e.OccurredAtUtc };
    public static void Apply(StormingNodeAdded e, DomainEventCatalog x) { if (e.NodeType == StormingNodeType.DomainEvent && !x.DomainEvents.Contains(e.Label, StringComparer.Ordinal)) x.DomainEvents.Add(e.Label); x.LastChangedAtUtc = e.OccurredAtUtc; }
    public static void Apply(StormingNodesConnected e, DomainEventCatalog x) => x.LastChangedAtUtc = e.OccurredAtUtc;
    public static void Apply(StormingHotspotMarked e, DomainEventCatalog x) => x.LastChangedAtUtc = e.OccurredAtUtc;
    public static void Apply(StormingNodeReordered e, DomainEventCatalog x) => x.LastChangedAtUtc = e.OccurredAtUtc;
}
