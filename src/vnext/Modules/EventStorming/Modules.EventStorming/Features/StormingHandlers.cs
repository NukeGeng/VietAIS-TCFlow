using Marten;
using VietAIS.TCFlow.BuildingBlocks.Application.Identity;
using VietAIS.TCFlow.BuildingBlocks.Application.Time;
using VietAIS.TCFlow.BuildingBlocks.EventSourcing.Metadata;
using VietAIS.TCFlow.Modules.EventStorming.Contracts.Commands;
using VietAIS.TCFlow.Modules.EventStorming.Domain;
using Wolverine.Attributes;

namespace VietAIS.TCFlow.Modules.EventStorming.Features;

public sealed record StormingCommandResult(Guid BoardId, long ExpectedVersion);

[WolverineHandler]
public static class StormingHandlers
{
    public static StormingCommandResult Handle(CreateBoard command, IDocumentSession session, IClock clock, IIdGenerator ids)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(command.ProjectId, Guid.Empty);
        var id = command.BoardId is { } requested && requested != Guid.Empty ? requested : ids.NewId();
        var name = Normalize(command.Name, 2, 200, nameof(command.Name));
        Validate(command.ActorId, command.CorrelationId);
        session.ApplyEventMetadata(new EventMetadata(command.ActorId, command.CorrelationId, command.CausationId, command.ProjectId, null, "event-storming.board.create"));
        session.Events.StartStream<StormingBoard>(id, new BoardCreated(id, command.ProjectId, name, command.ActorId.Trim(), command.CorrelationId.Trim(), clock.UtcNow));
        return new(id, 1);
    }

    public static Task<StormingCommandResult> Handle(AddNode c, IDocumentSession s, IClock k, CancellationToken ct) => Transition(c.BoardId, c.ExpectedVersion, c.ActorId, c.CorrelationId, c.CausationId, s, k, ct, (b, x) => b.AddNode(c.NodeId, c.NodeType, c.Label, c.Description, x.Actor, x.Correlation, x.Now));
    public static Task<StormingCommandResult> Handle(ConnectNodes c, IDocumentSession s, IClock k, CancellationToken ct) => Transition(c.BoardId, c.ExpectedVersion, c.ActorId, c.CorrelationId, c.CausationId, s, k, ct, (b, x) => b.Connect(c.FromNodeId, c.ToNodeId, c.Relationship, x.Actor, x.Correlation, x.Now));
    public static Task<StormingCommandResult> Handle(MarkHotspot c, IDocumentSession s, IClock k, CancellationToken ct) => Transition(c.BoardId, c.ExpectedVersion, c.ActorId, c.CorrelationId, c.CausationId, s, k, ct, (b, x) => b.MarkHotspot(c.NodeId, c.Reason, x.Actor, x.Correlation, x.Now));
    public static Task<StormingCommandResult> Handle(ReorderNode c, IDocumentSession s, IClock k, CancellationToken ct) => Transition(c.BoardId, c.ExpectedVersion, c.ActorId, c.CorrelationId, c.CausationId, s, k, ct, (b, x) => b.Reorder(c.NodeId, c.Position, x.Actor, x.Correlation, x.Now));

    private static async Task<StormingCommandResult> Transition(Guid boardId, long expectedVersion, string actor, string correlation, string? causation, IDocumentSession session, IClock clock, CancellationToken cancellationToken, Func<StormingBoard, Context, object> decide)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(boardId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedVersion);
        Validate(actor, correlation);
        var stream = await session.Events.FetchForWriting<StormingBoard>(boardId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (stream.Aggregate is null) throw new KeyNotFoundException($"Board '{boardId}' was not found.");
        session.ApplyEventMetadata(new EventMetadata(actor, correlation, causation, stream.Aggregate.ProjectId, null, "event-storming.board.change"));
        stream.AppendOne(decide(stream.Aggregate, new Context(actor.Trim(), correlation.Trim(), clock.UtcNow)));
        return new(boardId, expectedVersion + 1);
    }

    private sealed record Context(string Actor, string Correlation, DateTimeOffset Now);
    private static void Validate(string actor, string correlation) { ArgumentException.ThrowIfNullOrWhiteSpace(actor); ArgumentException.ThrowIfNullOrWhiteSpace(correlation); }
    private static string Normalize(string value, int min, int max, string name) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length < min || v.Length > max) throw new ArgumentException($"Value must contain between {min} and {max} characters.", name); return v; }
}
