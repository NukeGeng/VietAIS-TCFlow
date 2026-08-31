using Marten;
using VietAIS.TCFlow.BuildingBlocks.Application.Identity;
using VietAIS.TCFlow.BuildingBlocks.Application.Time;
using VietAIS.TCFlow.BuildingBlocks.EventSourcing.Metadata;
using VietAIS.TCFlow.Modules.RepositoryIntelligence.Contracts.Commands;
using VietAIS.TCFlow.Modules.RepositoryIntelligence.Domain;
using Wolverine.Attributes;

namespace VietAIS.TCFlow.Modules.RepositoryIntelligence.Features;

public sealed record AnalysisCommandResult(Guid AnalysisRunId, long ExpectedVersion);

[WolverineHandler]
public static class RepositoryHandlers
{
    public static AnalysisCommandResult Handle(StartAnalysis c, IDocumentSession s, IClock k, IIdGenerator ids)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(c.ProjectId, Guid.Empty); Validate(c.ActorId, c.CorrelationId);
        var id = c.AnalysisRunId is { } requested && requested != Guid.Empty ? requested : ids.NewId();
        s.ApplyEventMetadata(new EventMetadata(c.ActorId, c.CorrelationId, c.CausationId, c.ProjectId, null, "repository-intelligence.analysis.start"));
        s.Events.StartStream<AnalysisRun>(id, new AnalysisStarted(id, c.ProjectId, Text(c.RepositoryId, 1, 300), Text(c.CommitSha, 1, 200), c.ActorId.Trim(), c.CorrelationId.Trim(), k.UtcNow));
        return new(id, 1);
    }
    public static Task<AnalysisCommandResult> Handle(ObserveArtifact c, IDocumentSession s, IClock k, CancellationToken ct) => Transition(c.AnalysisRunId, c.ExpectedVersion, c.ActorId, c.CorrelationId, c.CausationId, s, k, ct, (a, x) => a.Observe(c.Path, c.Kind, c.Symbol, c.Details, x.Actor, x.Correlation, x.Now));
    public static Task<AnalysisCommandResult> Handle(DetectSourceChange c, IDocumentSession s, IClock k, CancellationToken ct) => Transition(c.AnalysisRunId, c.ExpectedVersion, c.ActorId, c.CorrelationId, c.CausationId, s, k, ct, (a, x) => a.DetectChange(c.ChangeKey, c.Path, c.ChangeType, c.Summary, x.Actor, x.Correlation, x.Now));
    public static Task<AnalysisCommandResult> Handle(RecordEvidence c, IDocumentSession s, IClock k, CancellationToken ct) => Transition(c.AnalysisRunId, c.ExpectedVersion, c.ActorId, c.CorrelationId, c.CausationId, s, k, ct, (a, x) => a.RecordEvidence(c.EvidenceKey, c.SourcePath, c.Claim, c.Confidence, x.Actor, x.Correlation, x.Now));
    public static Task<AnalysisCommandResult> Handle(CompleteAnalysis c, IDocumentSession s, IClock k, CancellationToken ct) => Transition(c.AnalysisRunId, c.ExpectedVersion, c.ActorId, c.CorrelationId, c.CausationId, s, k, ct, (a, x) => a.Complete(x.Actor, x.Correlation, x.Now));

    private static async Task<AnalysisCommandResult> Transition(Guid id, long version, string actor, string correlation, string? causation, IDocumentSession session, IClock clock, CancellationToken ct, Func<AnalysisRun, Context, object> decide)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty); ArgumentOutOfRangeException.ThrowIfNegative(version); Validate(actor, correlation);
        var stream = await session.Events.FetchForWriting<AnalysisRun>(id, version, ct).ConfigureAwait(false);
        if (stream.Aggregate is null) throw new KeyNotFoundException($"Analysis run '{id}' was not found.");
        session.ApplyEventMetadata(new EventMetadata(actor, correlation, causation, stream.Aggregate.ProjectId, null, "repository-intelligence.analysis.change"));
        stream.AppendOne(decide(stream.Aggregate, new Context(actor.Trim(), correlation.Trim(), clock.UtcNow)));
        return new(id, version + 1);
    }
    private sealed record Context(string Actor, string Correlation, DateTimeOffset Now);
    private static void Validate(string actor, string correlation) { ArgumentException.ThrowIfNullOrWhiteSpace(actor); ArgumentException.ThrowIfNullOrWhiteSpace(correlation); }
    private static string Text(string value, int min, int max) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var v = value.Trim(); if (v.Length < min || v.Length > max) throw new ArgumentException($"Value must contain between {min} and {max} characters.", nameof(value)); return v; }
}
