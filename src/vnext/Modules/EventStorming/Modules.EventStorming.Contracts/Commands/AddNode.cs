namespace VietAIS.TCFlow.Modules.EventStorming.Contracts.Commands;

public enum StormingNodeType { Command, DomainEvent, Aggregate, Actor, Policy, ReadModel, ExternalSystem, Hotspot, Note }

public sealed record AddNode(Guid BoardId, long ExpectedVersion, Guid NodeId, StormingNodeType NodeType, string Label, string? Description, string ActorId, string CorrelationId, string? CausationId = null);
