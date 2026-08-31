using VietAIS.TCFlow.Modules.EventStorming.Contracts.Commands;

namespace VietAIS.TCFlow.Modules.EventStorming.Domain;

public sealed record BoardCreated(Guid BoardId, Guid ProjectId, string Name, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record StormingNodeAdded(Guid BoardId, Guid NodeId, StormingNodeType NodeType, string Label, string? Description, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record StormingNodesConnected(Guid BoardId, Guid FromNodeId, Guid ToNodeId, string Relationship, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record StormingHotspotMarked(Guid BoardId, Guid NodeId, string Reason, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
public sealed record StormingNodeReordered(Guid BoardId, Guid NodeId, int Position, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
