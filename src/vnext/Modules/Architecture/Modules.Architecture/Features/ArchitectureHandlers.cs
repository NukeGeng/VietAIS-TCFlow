using Marten;
using VietAIS.TCFlow.BuildingBlocks.Application.Identity;
using VietAIS.TCFlow.BuildingBlocks.Application.Time;
using VietAIS.TCFlow.BuildingBlocks.EventSourcing.Metadata;
using VietAIS.TCFlow.Modules.Architecture.Contracts.Commands;
using VietAIS.TCFlow.Modules.Architecture.Domain;
using Wolverine.Attributes;

namespace VietAIS.TCFlow.Modules.Architecture.Features;

public sealed record ArchitectureCommandResult(Guid ModelId, long ExpectedVersion);

[WolverineHandler]
public static class ArchitectureHandlers
{
    public static ArchitectureCommandResult Handle(CreateArchitectureModel c, IDocumentSession s, IClock k, IIdGenerator ids)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(c.ProjectId, Guid.Empty); Validate(c.ActorId, c.CorrelationId);
        var id = c.ModelId is { } requested && requested != Guid.Empty ? requested : ids.NewId();
        s.ApplyEventMetadata(new EventMetadata(c.ActorId, c.CorrelationId, c.CausationId, c.ProjectId, null, "architecture.model.create"));
        s.Events.StartStream<ArchitectureModel>(id, new ArchitectureModelCreated(id, c.ProjectId, Text(c.Name, 2, 200), c.ActorId.Trim(), c.CorrelationId.Trim(), k.UtcNow));
        return new(id, 1);
    }

    public static Task<ArchitectureCommandResult> Handle(AddModule c, IDocumentSession s, IClock k, CancellationToken ct) => Transition(c.ModelId, c.ExpectedVersion, c.ActorId, c.CorrelationId, c.CausationId, s, k, ct, (m, x) => m.AddModule(c.ModuleId, c.Name, c.Description, x.Actor, x.Correlation, x.Now));
    public static Task<ArchitectureCommandResult> Handle(ConnectModules c, IDocumentSession s, IClock k, CancellationToken ct) => Transition(c.ModelId, c.ExpectedVersion, c.ActorId, c.CorrelationId, c.CausationId, s, k, ct, (m, x) => m.ConnectModules(c.FromModuleId, c.ToModuleId, c.Relationship, x.Actor, x.Correlation, x.Now));
    public static Task<ArchitectureCommandResult> Handle(AddDataEntity c, IDocumentSession s, IClock k, CancellationToken ct) => Transition(c.ModelId, c.ExpectedVersion, c.ActorId, c.CorrelationId, c.CausationId, s, k, ct, (m, x) => m.AddEntity(c.EntityId, c.Name, c.Description, x.Actor, x.Correlation, x.Now));
    public static Task<ArchitectureCommandResult> Handle(AddDataRelationship c, IDocumentSession s, IClock k, CancellationToken ct) => Transition(c.ModelId, c.ExpectedVersion, c.ActorId, c.CorrelationId, c.CausationId, s, k, ct, (m, x) => m.AddDataRelationship(c.FromEntityId, c.ToEntityId, c.Relationship, x.Actor, x.Correlation, x.Now));
    public static Task<ArchitectureCommandResult> Handle(RecordArchitectureDrift c, IDocumentSession s, IClock k, CancellationToken ct) => Transition(c.ModelId, c.ExpectedVersion, c.ActorId, c.CorrelationId, c.CausationId, s, k, ct, (m, x) => m.RecordDrift(c.DriftKey, c.Summary, c.Evidence, x.Actor, x.Correlation, x.Now));

    private static async Task<ArchitectureCommandResult> Transition(Guid id, long version, string actor, string correlation, string? causation, IDocumentSession session, IClock clock, CancellationToken ct, Func<ArchitectureModel, Context, object> decide)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty); ArgumentOutOfRangeException.ThrowIfNegative(version); Validate(actor, correlation);
        var stream = await session.Events.FetchForWriting<ArchitectureModel>(id, version, ct).ConfigureAwait(false);
        if (stream.Aggregate is null) throw new KeyNotFoundException($"Architecture model '{id}' was not found.");
        session.ApplyEventMetadata(new EventMetadata(actor, correlation, causation, stream.Aggregate.ProjectId, null, "architecture.model.change"));
        stream.AppendOne(decide(stream.Aggregate, new Context(actor.Trim(), correlation.Trim(), clock.UtcNow)));
        return new(id, version + 1);
    }
    private sealed record Context(string Actor, string Correlation, DateTimeOffset Now);
    private static void Validate(string actor, string correlation) { ArgumentException.ThrowIfNullOrWhiteSpace(actor); ArgumentException.ThrowIfNullOrWhiteSpace(correlation); }
    private static string Text(string value, int min, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length < min || v.Length > max) throw new ArgumentException($"Value must contain between {min} and {max} characters.", nameof(value)); return v; }
}
