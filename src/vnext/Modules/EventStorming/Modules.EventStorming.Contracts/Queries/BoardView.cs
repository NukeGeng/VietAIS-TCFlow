using VietAIS.TCFlow.Modules.EventStorming.Contracts.Commands;

namespace VietAIS.TCFlow.Modules.EventStorming.Contracts.Queries;

public sealed record StormingNodeView(Guid Id, StormingNodeType NodeType, string Label, string? Description, bool IsHotspot, int Position);
public sealed record StormingConnectionView(Guid FromNodeId, Guid ToNodeId, string Relationship);
public sealed record BoardView(Guid Id, Guid ProjectId, string Name, long Version, IReadOnlyList<StormingNodeView> Nodes, IReadOnlyList<StormingConnectionView> Connections, DateTimeOffset LastChangedAtUtc);
public sealed record DomainEventCatalogView(Guid Id, Guid ProjectId, string Name, IReadOnlyList<string> DomainEvents, DateTimeOffset LastChangedAtUtc);
