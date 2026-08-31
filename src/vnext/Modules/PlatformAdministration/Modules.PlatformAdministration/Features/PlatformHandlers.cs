using Marten;
using VietAIS.TCFlow.BuildingBlocks.Application.Time;
using VietAIS.TCFlow.BuildingBlocks.EventSourcing.Metadata;
using VietAIS.TCFlow.Modules.PlatformAdministration.Contracts.Commands;
using VietAIS.TCFlow.Modules.PlatformAdministration.Domain;
using Wolverine.Attributes;

namespace VietAIS.TCFlow.Modules.PlatformAdministration.Features;

public sealed record PlatformCommandResult(Guid PolicyId, long ExpectedVersion);

[WolverineHandler]
public static class PlatformHandlers
{
    public static async Task<PlatformCommandResult> Handle(UpdatePlatformPolicy c, IDocumentSession s, IClock k, CancellationToken ct)
    {
        Validate(c.PolicyId, c.ExpectedVersion, c.ActorId, c.CorrelationId);
        var stream = await s.Events.FetchForWriting<PlatformPolicy>(c.PolicyId, c.ExpectedVersion, ct).ConfigureAwait(false);
        if (stream.Aggregate is null)
        {
            if (c.ExpectedVersion != 0) throw new KeyNotFoundException($"Platform policy '{c.PolicyId}' was not found.");
            var policy = new PlatformPolicy();
            policy.Apply(new PlatformPolicyCreated(c.PolicyId, c.ActorId, c.CorrelationId, k.UtcNow));
            var update = policy.Update(c.AllowAiAnalysis, c.AllowAiTaskSuggestions, c.AllowAiTaskMutations, c.ActorId, c.CorrelationId, k.UtcNow);
            var audit = policy.Audit("platform.policy.update", c.ActorId, c.CorrelationId, k.UtcNow);
            s.ApplyEventMetadata(new EventMetadata(c.ActorId, c.CorrelationId, c.CausationId, null, null, "platform.policy.update"));
            s.Events.StartStream<PlatformPolicy>(c.PolicyId, new PlatformPolicyCreated(c.PolicyId, c.ActorId.Trim(), c.CorrelationId.Trim(), k.UtcNow), update, audit);
            return new(c.PolicyId, 3);
        }

        s.ApplyEventMetadata(new EventMetadata(c.ActorId, c.CorrelationId, c.CausationId, null, null, "platform.policy.update"));
        stream.AppendMany(stream.Aggregate.Update(c.AllowAiAnalysis, c.AllowAiTaskSuggestions, c.AllowAiTaskMutations, c.ActorId, c.CorrelationId, k.UtcNow), stream.Aggregate.Audit("platform.policy.update", c.ActorId, c.CorrelationId, k.UtcNow));
        return new(c.PolicyId, c.ExpectedVersion + 2);
    }

    public static async Task<PlatformCommandResult> Handle(ConfigureAiProvider c, IDocumentSession s, IClock k, CancellationToken ct)
    {
        Validate(c.PolicyId, c.ExpectedVersion, c.ActorId, c.CorrelationId);
        var stream = await s.Events.FetchForWriting<PlatformPolicy>(c.PolicyId, c.ExpectedVersion, ct).ConfigureAwait(false);
        if (stream.Aggregate is null) throw new KeyNotFoundException($"Platform policy '{c.PolicyId}' was not found.");
        s.ApplyEventMetadata(new EventMetadata(c.ActorId, c.CorrelationId, c.CausationId, null, null, "platform.ai-provider.configure"));
        stream.AppendMany(stream.Aggregate.ConfigureProvider(c.ProviderName, c.Enabled, c.ActorId, c.CorrelationId, k.UtcNow), stream.Aggregate.Audit("platform.ai-provider.configure", c.ActorId, c.CorrelationId, k.UtcNow));
        return new(c.PolicyId, c.ExpectedVersion + 2);
    }

    private static void Validate(Guid id, long version, string actor, string correlation) { ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty); ArgumentOutOfRangeException.ThrowIfNegative(version); ArgumentException.ThrowIfNullOrWhiteSpace(actor); ArgumentException.ThrowIfNullOrWhiteSpace(correlation); }
}
