using Marten;
using VietAIS.TCFlow.BuildingBlocks.Application.Identity;
using VietAIS.TCFlow.BuildingBlocks.Application.Time;
using VietAIS.TCFlow.BuildingBlocks.EventSourcing.Metadata;
using VietAIS.TCFlow.Modules.Planning.Contracts.Commands;
using VietAIS.TCFlow.Modules.Planning.Domain;
using Wolverine.Attributes;

namespace VietAIS.TCFlow.Modules.Planning.Features;

public sealed record PlanningCommandResult(Guid PlanId, long ExpectedVersion);

[WolverineHandler]
public static class PlanningHandlers
{
    public static PlanningCommandResult Handle(
        CreatePlan command,
        IDocumentSession session,
        IClock clock,
        IIdGenerator idGenerator)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(idGenerator);
        ValidateIdentity(command.ProjectId, command.ActorId, command.CorrelationId);
        var planId = command.PlanId == Guid.Empty ? idGenerator.NewId() : command.PlanId;
        var name = NormalizeName(command.Name);
        session.ApplyEventMetadata(new EventMetadata(
            command.ActorId,
            command.CorrelationId,
            command.CausationId,
            command.ProjectId,
            TenantId: null,
            Source: "planning.plan.create"));
        session.Events.StartStream<PlanAggregate>(
            planId,
            new PlanCreated(
                planId,
                command.ProjectId,
                name,
                NormalizePurpose(command.Purpose),
                command.ActorId.Trim(),
                command.CorrelationId.Trim(),
                clock.UtcNow));
        return new PlanningCommandResult(planId, 1);
    }

    public static async Task<PlanningCommandResult> Handle(
        AddRequirement command,
        IDocumentSession session,
        IClock clock,
        CancellationToken cancellationToken)
    {
        ValidateIdentity(command.PlanId, command.ActorId, command.CorrelationId);
        var stream = await session.Events.FetchForWriting<PlanAggregate>(
            command.PlanId,
            command.ExpectedVersion,
            cancellationToken).ConfigureAwait(false);
        if (stream.Aggregate is null)
        {
            throw new KeyNotFoundException($"Plan '{command.PlanId}' was not found.");
        }

        session.ApplyEventMetadata(new EventMetadata(
            command.ActorId,
            command.CorrelationId,
            command.CausationId,
            stream.Aggregate.ProjectId,
            TenantId: null,
            Source: "planning.requirement.add"));
        stream.AppendOne(stream.Aggregate.AddRequirement(
            command.RequirementId,
            command.Title,
            command.Description,
            command.ActorId,
            command.CorrelationId,
            clock.UtcNow));
        return new PlanningCommandResult(command.PlanId, command.ExpectedVersion + 1);
    }

    public static async Task<PlanningCommandResult> Handle(
        AddMilestone command,
        IDocumentSession session,
        IClock clock,
        CancellationToken cancellationToken)
    {
        ValidateIdentity(command.PlanId, command.ActorId, command.CorrelationId);
        var stream = await session.Events.FetchForWriting<PlanAggregate>(
            command.PlanId,
            command.ExpectedVersion,
            cancellationToken).ConfigureAwait(false);
        if (stream.Aggregate is null)
        {
            throw new KeyNotFoundException($"Plan '{command.PlanId}' was not found.");
        }

        session.ApplyEventMetadata(new EventMetadata(
            command.ActorId,
            command.CorrelationId,
            command.CausationId,
            stream.Aggregate.ProjectId,
            TenantId: null,
            Source: "planning.milestone.add"));
        stream.AppendOne(stream.Aggregate.AddMilestone(
            command.MilestoneId,
            command.Name,
            command.TargetDate,
            command.ActorId,
            command.CorrelationId,
            clock.UtcNow));
        return new PlanningCommandResult(command.PlanId, command.ExpectedVersion + 1);
    }

    private static void ValidateIdentity(Guid projectId, string actorId, string correlationId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(projectId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
    }

    private static string NormalizeName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length is < 2 or > 160)
        {
            throw new ArgumentException("Plan name must contain between 2 and 160 characters.", nameof(value));
        }

        return normalized;
    }

    private static string? NormalizePurpose(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > 1000)
        {
            throw new ArgumentException("Plan purpose cannot exceed 1000 characters.", nameof(value));
        }

        return normalized.Length == 0 ? null : normalized;
    }
}
